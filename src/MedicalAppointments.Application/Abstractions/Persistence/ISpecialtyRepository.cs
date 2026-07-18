using MedicalAppointments.Domain.Specialties;

namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface ISpecialtyRepository
{
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);

    void Add(Specialty specialty);

    Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Forces an UPDATE guarded by xmin even when Active won't change value, so a same-state PATCH still detects a stale version.
    void PrepareStatusUpdate(Specialty specialty, uint version);

    // Locks the specialty row (`SELECT ... FOR UPDATE`) so a concurrent activate/deactivate
    // serializes against it instead of racing. Must run inside an IUnitOfWork transaction to
    // hold the lock until the caller commits. Defaults to a non-locking read for repository
    // fakes that don't exercise this path.
    Task<Specialty?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetByIdAsync(id, cancellationToken);
}
