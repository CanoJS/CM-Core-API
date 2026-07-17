namespace MedicalAppointments.Application.Abstractions.Queries;

public interface ISpecialtyCatalogReader
{
    Task<IReadOnlyList<SpecialtyCatalogItem>> GetActiveAsync(CancellationToken cancellationToken);
}

public sealed record SpecialtyCatalogItem(Guid Id, string Name);
