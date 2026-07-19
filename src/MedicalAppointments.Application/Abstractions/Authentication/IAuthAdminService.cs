namespace MedicalAppointments.Application.Abstractions.Authentication;

public interface IAuthAdminService
{
    // Creates the user directly, already email-confirmed, with the given temporary password -
    // no invitation email is sent. Chosen over Supabase's invite flow (POST /auth/v1/invite) for
    // this MVP because invite sends a real email and is subject to Supabase's
    // over_email_send_rate_limit, which made bulk/demo doctor registration unreliable.
    Task<Guid> CreateDoctorUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        CancellationToken cancellationToken);

    // Best-effort compensation: never throws. Failures are logged internally, never exposed to callers.
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken);
}
