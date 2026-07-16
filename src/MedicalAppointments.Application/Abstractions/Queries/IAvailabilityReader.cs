namespace MedicalAppointments.Application.Abstractions.Queries;

public interface IAvailabilityReader
{
    Task<IReadOnlySet<DateTimeOffset>> GetOccupiedSlotsAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);
}
