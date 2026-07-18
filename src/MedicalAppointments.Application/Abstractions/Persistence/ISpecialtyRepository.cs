using MedicalAppointments.Domain.Specialties;

namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface ISpecialtyRepository
{
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);

    void Add(Specialty specialty);

    Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void SetVersion(Specialty specialty, uint version);
}
