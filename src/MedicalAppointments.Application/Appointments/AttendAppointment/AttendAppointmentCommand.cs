using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Appointments.AttendAppointment;

public sealed record AttendAppointmentCommand(
    Guid AppointmentId,
    string MedicalNote,
    string Version) : ICommand<AttendAppointmentResponse>;

public sealed record AttendAppointmentResponse(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason,
    string? MedicalNote,
    string Status,
    string Version);
