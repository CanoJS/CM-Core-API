using MedicalAppointments.Application.Abstractions.Scheduling;

namespace MedicalAppointments.Infrastructure.Scheduling;

public sealed class ClinicSchedule(TimeZoneInfo clinicTimeZone) : IClinicSchedule
{
    private static readonly TimeOnly OpenTime = new(8, 0);
    private static readonly TimeOnly CloseTime = new(18, 0);
    private const int SlotMinutes = 30;

    public bool IsBookableSlot(DateTimeOffset startsAt)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(startsAt, clinicTimeZone);
        TimeOnly time = TimeOnly.FromDateTime(local.DateTime);

        return IsBusinessDay(local.DayOfWeek)
            && time >= OpenTime
            && time < CloseTime
            && time.Minute % SlotMinutes == 0
            && time.Second == 0;
    }

    public IReadOnlyList<DateTimeOffset> GetBookableSlots(DateOnly localDate)
    {
        if (!IsBusinessDay(localDate.DayOfWeek))
        {
            return [];
        }

        DateTime localStart = localDate.ToDateTime(OpenTime, DateTimeKind.Unspecified);
        int slotCount = (int)(CloseTime - OpenTime).TotalMinutes / SlotMinutes;
        var slots = new DateTimeOffset[slotCount];

        for (int index = 0; index < slotCount; index++)
        {
            DateTime local = localStart.AddMinutes(index * SlotMinutes);
            slots[index] = new DateTimeOffset(local, clinicTimeZone.GetUtcOffset(local)).ToUniversalTime();
        }

        return slots;
    }

    private static bool IsBusinessDay(DayOfWeek dayOfWeek) =>
        dayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
}
