namespace MedicalAppointments.Application.Abstractions.Queries;

public interface IAdminSpecialtyReader
{
    Task<IReadOnlyList<AdminSpecialtyItem>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed record AdminSpecialtyItem(Guid Id, string Name, bool Active, uint Version);
