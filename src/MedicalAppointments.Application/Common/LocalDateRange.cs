namespace MedicalAppointments.Application.Common;

public static class LocalDateRange
{
    // Local calendar-day boundaries converted to UTC instants: `fromDate` at 00:00 local
    // (inclusive) through the day after `toDate` at 00:00 local (exclusive). Pure date/timezone
    // math (no EF/DbContext), so both Application query handlers and Infrastructure readers can
    // share it without Application depending on Infrastructure.
    public static (DateTimeOffset UtcFrom, DateTimeOffset UtcToExclusive) ToUtcBounds(
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
