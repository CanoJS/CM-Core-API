using MedicalAppointments.Domain.Specialties;

namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface ISpecialtyRepository
{
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);

    void Add(Specialty specialty);

    Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Forces an UPDATE guarded by xmin even when Active won't change value, so a same-state PATCH still detects a stale version.
    void PrepareStatusUpdate(Specialty specialty, uint version);
}
