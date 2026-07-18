namespace MedicalAppointments.Infrastructure.Authentication;

public sealed class SupabaseAuthAdminOptions
{
    public string? SecretKey { get; set; }

    public string? DoctorInviteRedirectUrl { get; set; }
}
