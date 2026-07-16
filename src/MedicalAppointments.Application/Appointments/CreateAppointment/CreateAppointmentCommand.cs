using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Appointments.CreateAppointment;

public sealed record CreateAppointmentCommand(
    Guid DoctorId,
    DateTimeOffset StartsAt,
    string Reason) : ICommand<CreateAppointmentResponse>;

public sealed record CreateAppointmentResponse(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason,
    string Status,
    uint Version);
