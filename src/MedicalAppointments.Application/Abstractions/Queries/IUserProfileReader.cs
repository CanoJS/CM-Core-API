using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Abstractions.Queries;

public interface IUserProfileReader
{
    Task<UserProfileSnapshot?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record UserProfileSnapshot(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool Active);
