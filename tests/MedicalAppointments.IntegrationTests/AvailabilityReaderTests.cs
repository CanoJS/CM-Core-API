using MedicalAppointments.Infrastructure.Persistence.Readers;

namespace MedicalAppointments.IntegrationTests;

// Covers AvailabilityReader.ComputeUtcRange - the local-day-boundary-to-UTC math - without a
// database. The LINQ query itself (filters by doctor/SCHEDULED, single query for the whole
// range) is verified by code review, not an automated test: medical.appointments.doctor_id and
// .patient_id both have foreign keys into medical.user_profiles / auth.users, and creating a
// real Supabase Auth user in an automated test is out of scope (the same limitation already
// documented on DoctorRepositoryConcurrencyTests). GetDoctorAvailabilityQueryHandlerTests covers
// the SCHEDULED/CANCELLED/other-doctor filtering behavior at the handler level using a fake
// IAvailabilityReader instead.
public sealed class AvailabilityReaderTests
{
    private static readonly TimeZoneInfo ClinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    // 2026-07-20 is a Monday (verified against a real calendar).
    private static readonly DateOnly Monday = new(2026, 7, 20);

    [Fact]
    public void ComputeUtcRange_SingleDay_FromIsInclusiveMidnightLocal()
    {
        (DateTimeOffset utcFrom, _) = AvailabilityReader.ComputeUtcRange(Monday, Monday, ClinicTimeZone);

        DateTimeOffset local = TimeZoneInfo.ConvertTime(utcFrom, ClinicTimeZone);
        Assert.Equal(Monday, DateOnly.FromDateTime(local.DateTime));
        Assert.Equal(TimeOnly.MinValue, TimeOnly.FromDateTime(local.DateTime));
    }

    [Fact]
    public void ComputeUtcRange_SingleDay_ToExclusiveIsMidnightLocalOfNextDay()
    {
        (_, DateTimeOffset utcToExclusive) = AvailabilityReader.ComputeUtcRange(Monday, Monday, ClinicTimeZone);

        DateTimeOffset local = TimeZoneInfo.ConvertTime(utcToExclusive, ClinicTimeZone);
        Assert.Equal(Monday.AddDays(1), DateOnly.FromDateTime(local.DateTime));
        Assert.Equal(TimeOnly.MinValue, TimeOnly.FromDateTime(local.DateTime));
    }

    [Fact]
    public void ComputeUtcRange_MultiDayRange_SpansFromFirstDayToDayAfterLast()
    {
        DateOnly to = Monday.AddDays(6);

        (DateTimeOffset utcFrom, DateTimeOffset utcToExclusive) =
            AvailabilityReader.ComputeUtcRange(Monday, to, ClinicTimeZone);

        Assert.Equal(TimeSpan.FromDays(7), utcToExclusive - utcFrom);
    }

    [Fact]
    public void ComputeUtcRange_UsesUtcOffsetFromInjectedClinicTimeZone_NotHardcoded()
    {
        var utcTimeZone = TimeZoneInfo.Utc;

        (DateTimeOffset utcFrom, _) = AvailabilityReader.ComputeUtcRange(Monday, Monday, utcTimeZone);

        Assert.Equal(Monday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), utcFrom.UtcDateTime);
    }
}
