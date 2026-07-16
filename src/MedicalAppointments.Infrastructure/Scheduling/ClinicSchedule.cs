using MedicalAppointments.Application.Abstractions.Scheduling;

namespace MedicalAppointments.Infrastructure.Scheduling;

public sealed class ClinicSchedule(TimeZoneInfo clinicTimeZone) : IClinicSchedule
{
    public bool IsBookableSlot(DateTimeOffset startsAt)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(startsAt, clinicTimeZone);
        TimeOnly time = TimeOnly.FromDateTime(local.DateTime);

        return local.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
            && time >= new TimeOnly(8, 0)
            && time < new TimeOnly(18, 0)
            && time.Minute % 30 == 0
            && time.Second == 0;
    }
}
