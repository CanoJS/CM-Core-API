using MedicalAppointments.Domain.Appointments;

namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface IAppointmentRepository
{
    Task<bool> HasScheduledAppointmentAsync(
        Guid doctorId,
        DateTimeOffset startsAt,
        CancellationToken cancellationToken);

    void Add(Appointment appointment);
}
