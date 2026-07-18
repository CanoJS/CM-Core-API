using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Appointments.RescheduleAppointment;

public sealed record RescheduleAppointmentCommand(
    Guid AppointmentId,
    Guid DoctorId,
    DateTimeOffset StartsAt,
    string Version) : ICommand<RescheduleAppointmentResponse>;

public sealed record RescheduleAppointmentResponse(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason,
    string Status,
    string Version);
