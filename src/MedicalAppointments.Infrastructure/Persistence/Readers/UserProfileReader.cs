using MedicalAppointments.Application.Abstractions.Queries;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class UserProfileReader(MedicalAppointmentsDbContext dbContext) : IUserProfileReader
{
    public Task<UserProfileSnapshot?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == userId)
            .Select(profile => new UserProfileSnapshot(
                profile.Id,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                profile.Role,
                profile.Active))
            .SingleOrDefaultAsync(cancellationToken);
}
