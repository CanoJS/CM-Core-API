using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Repositories;

public sealed class UserProfileRepository(MedicalAppointmentsDbContext dbContext) : IUserProfileRepository
{
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.UserProfiles.AnyAsync(profile => profile.Email == email, cancellationToken);

    public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.UserProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
}
