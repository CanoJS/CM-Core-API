using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Appointments.GetAppointmentById;

public sealed record GetAppointmentByIdQuery(Guid AppointmentId) : IQuery<AppointmentResponse>;
