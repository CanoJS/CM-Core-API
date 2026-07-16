using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Appointments.CreateAppointment;

public sealed class CreateAppointmentCommandHandler(
    ICurrentUser currentUser,
    IClock clock,
    IClinicSchedule clinicSchedule,
    IDoctorRepository doctorRepository,
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateAppointmentCommand, CreateAppointmentResponse>
{
    public async Task<CreateAppointmentResponse> Handle(
        CreateAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Patient)
        {
            throw new ForbiddenException("Only patients can book appointments.");
        }

        if (!clinicSchedule.IsBookableSlot(command.StartsAt))
        {
            throw new ArgumentException(
                "Appointments must use a 30-minute slot from Monday to Friday, 08:00 to 18:00 clinic time.");
        }

        if (!await doctorRepository.IsActiveAsync(command.DoctorId, cancellationToken))
        {
            throw new NotFoundException("The selected doctor does not exist or is inactive.");
        }

        if (await appointmentRepository.HasScheduledAppointmentAsync(
                command.DoctorId,
                command.StartsAt,
                cancellationToken))
        {
            throw new ConflictException("The selected time slot is no longer available.");
        }

        Appointment appointment = Appointment.Schedule(
            currentUser.UserId,
            command.DoctorId,
            command.StartsAt,
            command.Reason,
            clock.UtcNow);

        appointmentRepository.Add(appointment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateAppointmentResponse(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.StartsAt,
            appointment.EndsAt,
            appointment.Reason,
            appointment.Status.ToString().ToUpperInvariant(),
            appointment.Version);
    }
}
