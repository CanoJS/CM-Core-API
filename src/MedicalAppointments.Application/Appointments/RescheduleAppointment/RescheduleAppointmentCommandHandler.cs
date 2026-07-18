using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Appointments.RescheduleAppointment;

public sealed class RescheduleAppointmentCommandHandler(
    ICurrentUser currentUser,
    IClock clock,
    IClinicSchedule clinicSchedule,
    IDoctorRepository doctorRepository,
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RescheduleAppointmentCommand, RescheduleAppointmentResponse>
{
    public async Task<RescheduleAppointmentResponse> Handle(
        RescheduleAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only administrators can reschedule appointments.");
        }

        if (!ConcurrencyToken.TryParse(command.Version, out uint version))
        {
            throw new ArgumentException("The appointment version is invalid.");
        }

        if (!clinicSchedule.IsBookableSlot(command.StartsAt))
        {
            throw new ArgumentException(
                "Appointments must use a 30-minute slot from Monday to Friday, 08:00 to 18:00 clinic time.");
        }

        Appointment appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId, cancellationToken)
            ?? throw new NotFoundException("The appointment does not exist.");

        if (!await doctorRepository.IsActiveAsync(command.DoctorId, cancellationToken))
        {
            throw new NotFoundException("The selected doctor does not exist or is inactive.");
        }

        // Informative precheck only, for a friendly message on the common case; excludes this
        // appointment's own row so keeping the same doctor/time doesn't self-conflict. The
        // partial unique index (ux_appointments_doctor_slot_scheduled) is still the definitive
        // guard against a concurrent reschedule/booking into the same slot.
        if (await appointmentRepository.HasScheduledAppointmentAsync(
                command.DoctorId,
                command.StartsAt,
                appointment.Id,
                cancellationToken))
        {
            throw new ConflictException("The selected time slot is no longer available.");
        }

        // Forces DoctorId/StartsAt as modified so a reschedule back onto the same doctor/time
        // still issues a real UPDATE - otherwise EF would see no value change and skip the xmin
        // check entirely, letting a stale version token silently "succeed".
        appointmentRepository.PrepareRescheduleUpdate(appointment, version);
        appointment.Reschedule(command.DoctorId, command.StartsAt, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RescheduleAppointmentResponse(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.StartsAt,
            appointment.EndsAt,
            appointment.Reason,
            appointment.Status.ToString().ToUpperInvariant(),
            ConcurrencyToken.ToToken(appointment.Version));
    }
}
