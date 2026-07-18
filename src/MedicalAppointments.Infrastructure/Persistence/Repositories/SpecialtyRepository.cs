using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Domain.Specialties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MedicalAppointments.Infrastructure.Persistence.Repositories;

public sealed class SpecialtyRepository(MedicalAppointmentsDbContext dbContext) : ISpecialtyRepository
{
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL lower(), matching ux_specialties_name; not executed client-side.
    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
        dbContext.Specialties.AnyAsync(
            specialty => specialty.Name.ToLower() == name.ToLower(),
            cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862

    public void Add(Specialty specialty) => dbContext.Specialties.Add(specialty);

    public Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Specialties.FirstOrDefaultAsync(specialty => specialty.Id == id, cancellationToken);

    public void PrepareStatusUpdate(Specialty specialty, uint version)
    {
        EntityEntry<Specialty> entry = dbContext.Entry(specialty);
        entry.Property(entity => entity.Active).IsModified = true;
        entry.Property(entity => entity.Version).OriginalValue = version;
    }
}
