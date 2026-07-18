using MedicalAppointments.Infrastructure.Scheduling;

namespace MedicalAppointments.IntegrationTests;

// Pure logic tests against the real Infrastructure.ClinicSchedule - no database needed.
public sealed class ClinicScheduleTests
{
    private static readonly TimeZoneInfo ClinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    private static readonly ClinicSchedule Schedule = new(ClinicTimeZone);

    // 2026-07-20 is a Monday (verified against a real calendar).
    private static readonly DateOnly Monday = new(2026, 7, 20);
    private static readonly DateOnly Saturday = new(2026, 7, 18);
    private static readonly DateOnly Sunday = new(2026, 7, 19);

    [Fact]
    public void IsBookableSlot_MondayAt0800Local_IsValid()
    {
        DateTimeOffset slot = LocalToUtc(Monday, new TimeOnly(8, 0));
        Assert.True(Schedule.IsBookableSlot(slot));
    }

    [Fact]
    public void IsBookableSlot_MondayAt1730Local_IsValid()
    {
        DateTimeOffset slot = LocalToUtc(Monday, new TimeOnly(17, 30));
        Assert.True(Schedule.IsBookableSlot(slot));
    }

    [Fact]
    public void IsBookableSlot_MondayAt1800Local_IsInvalid()
    {
        DateTimeOffset slot = LocalToUtc(Monday, new TimeOnly(18, 0));
        Assert.False(Schedule.IsBookableSlot(slot));
    }

    [Fact]
    public void IsBookableSlot_MondayBefore0800Local_IsInvalid()
    {
        DateTimeOffset slot = LocalToUtc(Monday, new TimeOnly(7, 30));
        Assert.False(Schedule.IsBookableSlot(slot));
    }

    [Theory]
    [MemberData(nameof(WeekendDates))]
    public void IsBookableSlot_OnWeekend_IsInvalid(DateOnly weekendDate)
    {
        DateTimeOffset slot = LocalToUtc(weekendDate, new TimeOnly(9, 0));
        Assert.False(Schedule.IsBookableSlot(slot));
    }

    [Theory]
    [InlineData(8, 15)]
    [InlineData(8, 45)]
    [InlineData(9, 10)]
    public void IsBookableSlot_WithMinutesNotOnHalfHourBoundary_IsInvalid(int hour, int minute)
    {
        DateTimeOffset slot = LocalToUtc(Monday, new TimeOnly(hour, minute));
        Assert.False(Schedule.IsBookableSlot(slot));
    }

    [Fact]
    public void IsBookableSlot_WithNonZeroSeconds_IsInvalid()
    {
        DateTimeOffset slot = LocalToUtc(Monday, new TimeOnly(8, 0, 1));
        Assert.False(Schedule.IsBookableSlot(slot));
    }

    [Fact]
    public void GetBookableSlots_OnBusinessDay_Returns20SlotsFrom0800To1730Local()
    {
        IReadOnlyList<DateTimeOffset> slots = Schedule.GetBookableSlots(Monday);

        Assert.Equal(20, slots.Count);
        Assert.Equal(new TimeOnly(8, 0), LocalTimeOf(slots[0]));
        Assert.Equal(new TimeOnly(17, 30), LocalTimeOf(slots[^1]));
        for (int index = 1; index < slots.Count; index++)
        {
            Assert.Equal(TimeSpan.FromMinutes(30), slots[index] - slots[index - 1]);
        }
    }

    [Theory]
    [MemberData(nameof(WeekendDates))]
    public void GetBookableSlots_OnWeekend_ReturnsEmpty(DateOnly weekendDate)
    {
        Assert.Empty(Schedule.GetBookableSlots(weekendDate));
    }

    public static TheoryData<DateOnly> WeekendDates => new() { Saturday, Sunday };

    private static DateTimeOffset LocalToUtc(DateOnly date, TimeOnly time)
    {
        DateTime local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, ClinicTimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static TimeOnly LocalTimeOf(DateTimeOffset utc) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, ClinicTimeZone).DateTime);
}
