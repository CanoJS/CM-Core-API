using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Appointments.CancelAppointment;

public sealed class CancelAppointmentCommandHandler(
    ICurrentUser currentUser,
    IClock clock,
    IAppointmentRepository appointmentRepository,
    IUserProfileRepository userProfileRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CancelAppointmentCommand, CancelAppointmentResponse>
{
    public async Task<CancelAppointmentResponse> Handle(
        CancelAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role is not (UserRole.Patient or UserRole.Admin))
        {
            throw new ForbiddenException("Only patients and administrators can cancel appointments.");
        }

        // A deactivated patient's existing JWT keeps its PATIENT role claim until the token is
        // refreshed (the custom access token hook only recomputes user_role on refresh), so the
        // role check above alone cannot reject them. Active is the authoritative flag. ADMIN is
        // unaffected: its behavior for cancelling is unchanged.
        if (currentUser.Role == UserRole.Patient)
        {
            UserProfile patientProfile =
                await userProfileRepository.GetByIdAsync(currentUser.UserId, cancellationToken)
                    ?? throw new NotFoundException("The authenticated user profile does not exist.");

            if (!patientProfile.Active)
            {
                throw new ForbiddenException("Inactive accounts cannot cancel appointments.");
            }
        }

        if (!ConcurrencyToken.TryParse(command.Version, out uint version))
        {
            throw new ArgumentException("The appointment version is invalid.");
        }

        Appointment appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId, cancellationToken)
            ?? throw new NotFoundException("The appointment does not exist.");

        // A patient cancelling someone else's appointment gets the same 404 as a missing one -
        // never reveals that the id belongs to another patient.
        if (currentUser.Role == UserRole.Patient && appointment.PatientId != currentUser.UserId)
        {
            throw new NotFoundException("The appointment does not exist.");
        }

        appointmentRepository.PrepareStatusUpdate(appointment, version);

        DateTimeOffset now = clock.UtcNow;
        if (currentUser.Role == UserRole.Patient)
        {
            appointment.CancelByPatient(now);
        }
        else
        {
            appointment.CancelByAdmin(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CancelAppointmentResponse(
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
