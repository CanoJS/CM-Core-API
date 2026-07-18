namespace MedicalAppointments.Application.Abstractions.Scheduling;

public interface IClinicSchedule
{
    bool IsBookableSlot(DateTimeOffset startsAt);

    // UTC starts-at instant of every 30-minute bookable slot for the given clinic-local
    // calendar date, ascending. Empty on non-business days (weekends). The single source of
    // truth for clinic hours/slot duration, so Application code never re-derives 08:00/18:00/
    // 20 blocks by hand.
    IReadOnlyList<DateTimeOffset> GetBookableSlots(DateOnly localDate);
}
