using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Appointments.GetMyAppointments;

public sealed record GetMyAppointmentsQuery(
    string? Status,
    DateOnly? From,
    DateOnly? To,
    string? PatientName) : IQuery<IReadOnlyList<AppointmentResponse>>;
