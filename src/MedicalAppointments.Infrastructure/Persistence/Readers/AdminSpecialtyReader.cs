using MedicalAppointments.Application.Abstractions.Queries;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class AdminSpecialtyReader(MedicalAppointmentsDbContext dbContext) : IAdminSpecialtyReader
{
    public async Task<IReadOnlyList<AdminSpecialtyItem>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Specialties
            .AsNoTracking()
            .OrderBy(specialty => specialty.Name)
            .Select(specialty => new AdminSpecialtyItem(
                specialty.Id,
                specialty.Name,
                specialty.Active,
                specialty.Version))
            .ToArrayAsync(cancellationToken);
}
