namespace MedicalAppointments.Application.Abstractions.Queries;

public interface IDoctorCatalogReader
{
    Task<IReadOnlyList<DoctorCatalogItem>> GetActiveAsync(
        Guid? specialtyId,
        CancellationToken cancellationToken);
}

public sealed record DoctorCatalogItem(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    Guid SpecialtyId,
    string SpecialtyName,
    bool Active);
