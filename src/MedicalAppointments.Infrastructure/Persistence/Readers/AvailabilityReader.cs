using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class AvailabilityReader(
    MedicalAppointmentsDbContext dbContext,
    TimeZoneInfo clinicTimeZone) : IAvailabilityReader
{
    public async Task<IReadOnlySet<DateTimeOffset>> GetOccupiedSlotsAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        (DateTimeOffset utcFrom, DateTimeOffset utcToExclusive) = ComputeUtcRange(fromDate, toDate, clinicTimeZone);

        DateTimeOffset[] occupied = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctorId
                && appointment.Status == AppointmentStatus.Scheduled
                && appointment.StartsAt >= utcFrom
                && appointment.StartsAt < utcToExclusive)
            .Select(appointment => appointment.StartsAt)
            .ToArrayAsync(cancellationToken);

        return occupied.ToHashSet();
    }

    // Kept as a public static wrapper (existing tests/call sites use this name) delegating to
    // the shared Application.Common.LocalDateRange helper, so the same boundary math is not
    // duplicated for GetMyAppointments' date filters.
    public static (DateTimeOffset UtcFrom, DateTimeOffset UtcToExclusive) ComputeUtcRange(
        DateOnly fromDate,
        DateOnly toDate,
        TimeZoneInfo clinicTimeZone) =>
        LocalDateRange.ToUtcBounds(fromDate, toDate, clinicTimeZone);
}
