using MedicalAppointments.Application.Abstractions.Queries;
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

    // Local calendar-day boundaries converted to UTC instants: `fromDate` at 00:00 local
    // (inclusive) through the day after `toDate` at 00:00 local (exclusive). Public and static
    // so the boundary math is unit-testable without a DbContext/database.
    public static (DateTimeOffset UtcFrom, DateTimeOffset UtcToExclusive) ComputeUtcRange(
        DateOnly fromDate,
        DateOnly toDate,
        TimeZoneInfo clinicTimeZone)
    {
        DateTime localFrom = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        DateTime localToExclusive = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        DateTimeOffset utcFrom = new DateTimeOffset(localFrom, clinicTimeZone.GetUtcOffset(localFrom)).ToUniversalTime();
        DateTimeOffset utcToExclusive =
            new DateTimeOffset(localToExclusive, clinicTimeZone.GetUtcOffset(localToExclusive)).ToUniversalTime();

        return (utcFrom, utcToExclusive);
    }
}
