using MedicalAppointments.Domain.Common;

namespace MedicalAppointments.Domain.Appointments;

public sealed class Appointment
{
    public const int DurationMinutes = 30;

    private Appointment()
    {
    }

    private Appointment(
        Guid id,
        Guid patientId,
        Guid doctorId,
        DateTimeOffset startsAt,
        string reason)
    {
        Id = id;
        PatientId = patientId;
        DoctorId = doctorId;
        StartsAt = startsAt.ToUniversalTime();
        Reason = reason.Trim();
        Status = AppointmentStatus.Scheduled;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Guid DoctorId { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt => StartsAt.AddMinutes(DurationMinutes);

    public string Reason { get; private set; } = string.Empty;

    public AppointmentStatus Status { get; private set; }

    public string? MedicalNote { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public uint Version { get; private set; }

    public static Appointment Schedule(
        Guid patientId,
        Guid doctorId,
        DateTimeOffset startsAt,
        string reason,
        DateTimeOffset now)
    {
        if (patientId == Guid.Empty || doctorId == Guid.Empty)
        {
            throw new DomainException("Patient and doctor are required.");
        }

        if (startsAt <= now)
        {
            throw new DomainException("The appointment must be scheduled in the future.");
        }

        if (startsAt.Minute % DurationMinutes != 0 || startsAt.Second != 0)
        {
            throw new DomainException("The appointment must start on a 30-minute boundary.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw new DomainException("Reason is required and cannot exceed 500 characters.");
        }

        return new Appointment(Guid.NewGuid(), patientId, doctorId, startsAt, reason);
    }

    public void CancelByPatient(DateTimeOffset now)
    {
        EnsureScheduled();

        if (StartsAt - now <= TimeSpan.FromHours(24))
        {
            throw new DomainException("Patients can only cancel more than 24 hours in advance.");
        }

        Cancel(now);
    }

    public void CancelByAdmin(DateTimeOffset now)
    {
        EnsureScheduled();
        Cancel(now);
    }

    public void Reschedule(Guid doctorId, DateTimeOffset startsAt, DateTimeOffset now)
    {
        EnsureScheduled();

        if (doctorId == Guid.Empty || startsAt <= now)
        {
            throw new DomainException("A valid doctor and future start time are required.");
        }

        if (startsAt.Minute % DurationMinutes != 0 || startsAt.Second != 0)
        {
            throw new DomainException("The appointment must start on a 30-minute boundary.");
        }

        DoctorId = doctorId;
        StartsAt = startsAt.ToUniversalTime();
        UpdatedAt = now;
    }

    public void Attend(string medicalNote, DateTimeOffset now)
    {
        EnsureScheduled();

        if (string.IsNullOrWhiteSpace(medicalNote) || medicalNote.Trim().Length > 4_000)
        {
            throw new DomainException("Medical note is required and cannot exceed 4000 characters.");
        }

        MedicalNote = medicalNote.Trim();
        Status = AppointmentStatus.Attended;
        UpdatedAt = now;
    }

    private void Cancel(DateTimeOffset now)
    {
        Status = AppointmentStatus.Cancelled;
        UpdatedAt = now;
    }

    private void EnsureScheduled()
    {
        if (Status != AppointmentStatus.Scheduled)
        {
            throw new DomainException("Only scheduled appointments can be modified.");
        }
    }
}
