using MedicalAppointments.Domain.Appointments;

namespace MedicalAppointments.Application.Abstractions.Queries;

public interface IAppointmentReader
{
    // Exactly one of PatientId/DoctorId set scopes the result to that patient/doctor; neither
    // set means every appointment (ADMIN only - enforced by the caller, not the reader).
    // patientNameContains is a partial, case-insensitive match against the patient's full name;
    // the caller (GetMyAppointmentsQueryHandler) only ever populates it for a DOCTOR-scoped call.
    Task<IReadOnlyList<AppointmentListItem>> GetAsync(
        Guid? patientId,
        Guid? doctorId,
        AppointmentStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        string? patientNameContains,
        CancellationToken cancellationToken);

    Task<AppointmentListItem?> GetByIdAsync(Guid appointmentId, CancellationToken cancellationToken);
}

public sealed record AppointmentListItem(
    Guid Id,
    Guid PatientId,
    string PatientFirstName,
    string PatientLastName,
    Guid DoctorId,
    string DoctorFirstName,
    string DoctorLastName,
    Guid SpecialtyId,
    string SpecialtyName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    AppointmentStatus Status,
    string Reason,
    string? MedicalNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
