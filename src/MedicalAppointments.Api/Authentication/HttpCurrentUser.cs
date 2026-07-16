using System.Security.Claims;
using System.Text.Json;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Api.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User
        ?? throw new UnauthorizedAccessException("An authenticated request is required.");

    public Guid UserId
    {
        get
        {
            string subject = Principal.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("The access token has no subject claim.");
            return Guid.Parse(subject);
        }
    }

    public UserRole Role
    {
        get
        {
            string? role = Principal.FindFirstValue("user_role") ?? GetRoleFromAppMetadata();

            return role?.ToUpperInvariant() switch
            {
                "PATIENT" => UserRole.Patient,
                "DOCTOR" => UserRole.Doctor,
                "ADMIN" => UserRole.Admin,
                _ => throw new UnauthorizedAccessException("The access token has no valid application role."),
            };
        }
    }

    private string? GetRoleFromAppMetadata()
    {
        string? metadata = Principal.FindFirstValue("app_metadata");
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(metadata);
        return document.RootElement.TryGetProperty("role", out JsonElement role)
            ? role.GetString()
            : null;
    }
}
