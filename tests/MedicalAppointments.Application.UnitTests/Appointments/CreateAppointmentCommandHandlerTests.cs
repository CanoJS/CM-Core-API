using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Application.Appointments.CreateAppointment;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Appointments;

public sealed class CreateAppointmentCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenSlotIsAvailable_CreatesAppointment()
    {
        var appointments = new AppointmentRepositoryStub();
        var handler = CreateHandler(appointments, hasConflict: false);
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
            "Control anual");

        CreateAppointmentResponse response = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("SCHEDULED", response.Status);
        Assert.NotNull(appointments.Added);
    }

    [Fact]
    public async Task Handle_WhenSlotWasTaken_ThrowsConflict()
    {
        var handler = CreateHandler(new AppointmentRepositoryStub(), hasConflict: true);
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
            "Control anual");

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    private static CreateAppointmentCommandHandler CreateHandler(
        AppointmentRepositoryStub appointments,
        bool hasConflict)
    {
        appointments.HasConflict = hasConflict;
        return new CreateAppointmentCommandHandler(
            new CurrentUserStub(),
            new ClockStub(),
            new ClinicScheduleStub(),
            new DoctorRepositoryStub(),
            appointments,
            new UnitOfWorkStub());
    }

    private sealed class CurrentUserStub : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => UserRole.Patient;
    }

    private sealed class ClockStub : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class ClinicScheduleStub : IClinicSchedule
    {
        public bool IsBookableSlot(DateTimeOffset startsAt) => true;
    }

    private sealed class DoctorRepositoryStub : IDoctorRepository
    {
        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class AppointmentRepositoryStub : IAppointmentRepository
    {
        public bool HasConflict { get; set; }

        public Appointment? Added { get; private set; }

        public Task<bool> HasScheduledAppointmentAsync(
            Guid doctorId,
            DateTimeOffset startsAt,
            CancellationToken cancellationToken) => Task.FromResult(HasConflict);

        public void Add(Appointment appointment) => Added = appointment;
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
