using MedicalAppointments.Domain.Appointments;

namespace MedicalAppointments.Application.Abstractions.Queries;

public interface IAdminDashboardReader
{
    Task<int> CountAppointmentsAsync(
        AppointmentStatus status,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtcExclusive,
        CancellationToken cancellationToken);
}
