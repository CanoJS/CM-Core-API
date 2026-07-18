using MedicalAppointments.Application.Abstractions.Queries;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class SpecialtyCatalogReader(MedicalAppointmentsDbContext dbContext)
    : ISpecialtyCatalogReader
{
    public async Task<IReadOnlyList<SpecialtyCatalogItem>> GetActiveAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Specialties
            .AsNoTracking()
            .Where(specialty => specialty.Active)
            .OrderBy(specialty => specialty.Name)
            .Select(specialty => new SpecialtyCatalogItem(specialty.Id, specialty.Name))
            .ToArrayAsync(cancellationToken);
}
