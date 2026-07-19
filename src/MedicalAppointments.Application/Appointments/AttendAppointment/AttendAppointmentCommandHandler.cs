using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Appointments.AttendAppointment;

public sealed class AttendAppointmentCommandHandler(
    ICurrentUser currentUser,
    IClock clock,
    IDoctorRepository doctorRepository,
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AttendAppointmentCommand, AttendAppointmentResponse>
{
    public async Task<AttendAppointmentResponse> Handle(
        AttendAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Doctor)
        {
            throw new ForbiddenException("Only doctors can attend appointments.");
        }

        if (!ConcurrencyToken.TryParse(command.Version, out uint version))
        {
            throw new ArgumentException("The appointment version is invalid.");
        }

        // The authenticated user's id (JWT sub) is never assumed to equal medical.doctors.id -
        // it must be resolved through the doctor's own user profile.
        Doctor doctor = await doctorRepository.GetByUserIdAsync(currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("The appointment does not exist.");

        // A deactivated doctor's existing JWT keeps its DOCTOR role claim until the token is
        // refreshed, so the role check above alone cannot reject them. ChangeDoctorStatusCommand
        // is the only place that ever changes this flag, and it always toggles the doctor's
        // profile.Active in the same transaction, so doctor.Active is an authoritative proxy for
        // profile status here - no extra IUserProfileRepository round trip is needed.
        if (!doctor.Active)
        {
            throw new ForbiddenException("Inactive doctor accounts cannot attend appointments.");
        }

        Appointment appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId, cancellationToken)
            ?? throw new NotFoundException("The appointment does not exist.");

        // Not assigned to this doctor gets the same 404 as a missing appointment - never reveals
        // that the id belongs to another doctor's schedule.
        if (appointment.DoctorId != doctor.Id)
        {
            throw new NotFoundException("The appointment does not exist.");
        }

        appointmentRepository.PrepareStatusUpdate(appointment, version);
        appointment.Attend(command.MedicalNote, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AttendAppointmentResponse(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.StartsAt,
            appointment.EndsAt,
            appointment.Reason,
            appointment.MedicalNote,
            appointment.Status.ToString().ToUpperInvariant(),
            ConcurrencyToken.ToToken(appointment.Version));
    }
}
