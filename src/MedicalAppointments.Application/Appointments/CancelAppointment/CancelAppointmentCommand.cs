using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Appointments.CancelAppointment;

public sealed record CancelAppointmentCommand(Guid AppointmentId, string Version) : ICommand<CancelAppointmentResponse>;

public sealed record CancelAppointmentResponse(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason,
    string Status,
    string Version);
