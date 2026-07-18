namespace MedicalAppointments.Application.Abstractions.Queries;

public interface IAdminDoctorReader
{
    Task<IReadOnlyList<AdminDoctorItem>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed record AdminDoctorItem(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    Guid SpecialtyId,
    string SpecialtyName,
    bool Active,
    uint Version);
