using MedicalAppointments.Domain.Appointments;

namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface IAppointmentRepository
{
    // excludeAppointmentId lets a reschedule check availability without tripping over the
    // appointment's own current row when the new slot happens to match the old one.
    Task<bool> HasScheduledAppointmentAsync(
        Guid doctorId,
        DateTimeOffset startsAt,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken);

    void Add(Appointment appointment);

    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Forces Status as modified and pins xmin's OriginalValue to the client-submitted version,
    // used by cancel/attend. Without this, GetByIdAsync's freshly-read xmin would silently
    // become the concurrency check's OriginalValue instead of what the client believed it was -
    // comparing current-vs-current always "succeeds", defeating the whole check.
    void PrepareStatusUpdate(Appointment appointment, uint version);

    // Same rationale as PrepareStatusUpdate, but forces DoctorId/StartsAt as modified: a
    // reschedule that keeps the same doctor and time must still issue a real UPDATE so a stale
    // xmin token is still caught.
    void PrepareRescheduleUpdate(Appointment appointment, uint version);
}
