using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Admin.GetAdminDashboard;

public sealed class GetAdminDashboardQueryHandler(
    ICurrentUser currentUser,
    IAdminDashboardReader dashboardReader,
    IClock clock,
    TimeZoneInfo clinicTimeZone)
    : IQueryHandler<GetAdminDashboardQuery, AdminDashboardResponse>
{
    public async Task<AdminDashboardResponse> Handle(
        GetAdminDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only administrators can view the dashboard.");
        }

        // "Today" is the clinic's local calendar day, derived from IClock (never
        // DateTimeOffset.UtcNow directly) so tests can pin the clock and the boundary follows
        // Clinic:TimeZone rather than a hardcoded offset.
        DateTimeOffset nowLocal = TimeZoneInfo.ConvertTime(clock.UtcNow, clinicTimeZone);
        DateOnly today = DateOnly.FromDateTime(nowLocal.DateTime);
        (DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive) =
            LocalDateRange.ToUtcBounds(today, today, clinicTimeZone);

        int scheduledToday = await dashboardReader.CountAppointmentsAsync(
            AppointmentStatus.Scheduled,
            fromUtc,
            toUtcExclusive,
            cancellationToken);

        return new AdminDashboardResponse(scheduledToday);
    }
}
