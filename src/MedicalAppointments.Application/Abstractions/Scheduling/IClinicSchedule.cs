namespace MedicalAppointments.Application.Abstractions.Scheduling;

public interface IClinicSchedule
{
    bool IsBookableSlot(DateTimeOffset startsAt);
}
