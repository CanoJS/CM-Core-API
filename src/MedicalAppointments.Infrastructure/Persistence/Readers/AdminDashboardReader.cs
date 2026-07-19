using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class AdminDashboardReader(MedicalAppointmentsDbContext dbContext) : IAdminDashboardReader
{
    // A single COUNT(*) with no joins and no row materialization - the dashboard only ever needs
    // the number, never the appointments themselves.
    public Task<int> CountAppointmentsAsync(
        AppointmentStatus status,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtcExclusive,
        CancellationToken cancellationToken) =>
        dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.Status == status
                && appointment.StartsAt >= fromUtc
                && appointment.StartsAt < toUtcExclusive)
            .CountAsync(cancellationToken);
}
