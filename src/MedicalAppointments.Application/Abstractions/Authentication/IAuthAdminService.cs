namespace MedicalAppointments.Application.Abstractions.Authentication;

public interface IAuthAdminService
{
    Task<Guid> InviteDoctorAsync(
        string email,
        string firstName,
        string lastName,
        CancellationToken cancellationToken);

    // Best-effort compensation: never throws. Failures are logged internally, never exposed to callers.
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken);
}
