using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface IUserProfileRepository
{
    // Callers must pass an already-lowercased email; matches the DB and domain normalization.
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
