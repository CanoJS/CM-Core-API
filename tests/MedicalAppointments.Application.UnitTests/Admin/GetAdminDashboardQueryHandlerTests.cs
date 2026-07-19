using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Admin.GetAdminDashboard;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Admin;

public sealed class GetAdminDashboardQueryHandlerTests
{
    private static readonly TimeZoneInfo ClinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    [Fact]
    public async Task Handle_WhenAdmin_ReturnsScheduledCountFromReader()
    {
        var reader = new AdminDashboardReaderStub(7);
        var handler = CreateHandler(UserRole.Admin, reader, new FixedClock(new DateTimeOffset(2026, 7, 20, 18, 0, 0, TimeSpan.Zero)));

        AdminDashboardResponse response = await handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        Assert.Equal(7, response.ScheduledToday);
        Assert.Equal(AppointmentStatus.Scheduled, reader.LastStatus);
    }

    [Theory]
    [InlineData(UserRole.Patient)]
    [InlineData(UserRole.Doctor)]
    public async Task Handle_WhenNotAdmin_ThrowsForbidden(UserRole role)
    {
        var handler = CreateHandler(role, new AdminDashboardReaderStub(0), new FixedClock(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UsesClinicTimeZoneToComputeTodayBounds_NotUtcCalendarDay()
    {
        // 2026-07-20 23:30 UTC is already 2026-07-21 in a zone west of Greenwich? No - Mexico
        // City is UTC-6/-5, so this UTC instant is still 2026-07-20 local (17:30/18:30). Pick an
        // instant where the UTC calendar day and the clinic-local calendar day genuinely differ:
        // 2026-07-21T04:00:00Z is 2026-07-20 (late evening) in America/Mexico_City.
        var clock = new FixedClock(new DateTimeOffset(2026, 7, 21, 4, 0, 0, TimeSpan.Zero));
        var reader = new AdminDashboardReaderStub(0);
        var handler = CreateHandler(UserRole.Admin, reader, clock);

        await handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        DateTimeOffset expectedFromUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Unspecified), ClinicTimeZone);
        DateTimeOffset expectedToUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Unspecified), ClinicTimeZone);

        Assert.Equal(expectedFromUtc, reader.LastFromUtc);
        Assert.Equal(expectedToUtcExclusive, reader.LastToUtcExclusive);
    }

    private static GetAdminDashboardQueryHandler CreateHandler(
        UserRole role,
        AdminDashboardReaderStub reader,
        IClock clock) =>
        new(new CurrentUserStub(role), reader, clock, ClinicTimeZone);

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class AdminDashboardReaderStub(int count) : IAdminDashboardReader
    {
        public AppointmentStatus? LastStatus { get; private set; }

        public DateTimeOffset? LastFromUtc { get; private set; }

        public DateTimeOffset? LastToUtcExclusive { get; private set; }

        public Task<int> CountAppointmentsAsync(
            AppointmentStatus status,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtcExclusive,
            CancellationToken cancellationToken)
        {
            LastStatus = status;
            LastFromUtc = fromUtc;
            LastToUtcExclusive = toUtcExclusive;
            return Task.FromResult(count);
        }
    }
}
