using MedicalAppointments.Domain.Appointments;

namespace MedicalAppointments.Application.Abstractions.Queries;

public interface IAppointmentReader
{
    // Exactly one of PatientId/DoctorId set scopes the result to that patient/doctor; neither
    // set means every appointment (ADMIN only - enforced by the caller, not the reader).
    Task<IReadOnlyList<AppointmentListItem>> GetAsync(
        Guid? patientId,
        Guid? doctorId,
        AppointmentStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
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
