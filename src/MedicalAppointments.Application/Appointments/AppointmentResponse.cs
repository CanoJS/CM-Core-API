using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Appointments;

public sealed record AppointmentResponse(
    Guid Id,
    Guid PatientId,
    string PatientFirstName,
    string PatientLastName,
    string PatientName,
    Guid DoctorId,
    string DoctorFirstName,
    string DoctorLastName,
    string DoctorName,
    Guid SpecialtyId,
    string SpecialtyName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status,
    string Reason,
    string? MedicalNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Version);

public static class AppointmentResponseMapper
{
    // docs/SECURITY.md: "La nota médica no debe aparecer en respuestas de paciente, recepción o
    // dashboard." The system has only PATIENT/DOCTOR/ADMIN roles (no separate reception/
    // dashboard role), so this reduces to: PATIENT never sees it; DOCTOR (their own patient) and
    // ADMIN may.
    public static AppointmentResponse ToResponse(AppointmentListItem item, UserRole viewerRole) =>
        new(
            item.Id,
            item.PatientId,
            item.PatientFirstName,
            item.PatientLastName,
            $"{item.PatientFirstName} {item.PatientLastName}",
            item.DoctorId,
            item.DoctorFirstName,
            item.DoctorLastName,
            $"{item.DoctorFirstName} {item.DoctorLastName}",
            item.SpecialtyId,
            item.SpecialtyName,
            item.StartsAt,
            item.EndsAt,
            item.Status.ToString().ToUpperInvariant(),
            item.Reason,
            viewerRole == UserRole.Patient ? null : item.MedicalNote,
            item.CreatedAt,
            item.UpdatedAt,
            ConcurrencyToken.ToToken(item.Version));
}
