using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Idempotency;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Application.Appointments.CreateAppointment;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Appointments;

public sealed class CreateAppointmentCommandHandlerTests
{
    private static readonly Guid CurrentUserId = Guid.NewGuid();

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
        Assert.Equal("0", response.Version);
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

    [Fact]
    public async Task Handle_WithIdempotencyKey_FirstCall_StagesRecordAndCreates()
    {
        var appointments = new AppointmentRepositoryStub();
        var idempotencyStore = new FakeIdempotencyStore();
        var handler = CreateHandler(appointments, hasConflict: false, idempotencyStore: idempotencyStore);
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
            "Control anual",
            "key-1");

        await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(appointments.Added);
        Assert.True(idempotencyStore.HasRecordFor(CurrentUserId, "key-1"));
    }

    [Fact]
    public async Task Handle_WithIdempotencyKey_SameKeyAndPayload_ReplaysWithoutCreatingAgain()
    {
        var appointments = new AppointmentRepositoryStub();
        var idempotencyStore = new FakeIdempotencyStore();
        var handler = CreateHandler(appointments, hasConflict: false, idempotencyStore: idempotencyStore);
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
            "Control anual",
            "key-1");

        CreateAppointmentResponse first = await handler.Handle(command, CancellationToken.None);
        appointments.Added = null;
        CreateAppointmentResponse second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Version, second.Version);
        Assert.Null(appointments.Added);
    }

    [Fact]
    public async Task Handle_WithIdempotencyKey_SameKeyDifferentPayload_ThrowsConflict()
    {
        var appointments = new AppointmentRepositoryStub();
        var idempotencyStore = new FakeIdempotencyStore();
        var handler = CreateHandler(appointments, hasConflict: false, idempotencyStore: idempotencyStore);
        Guid doctorId = Guid.NewGuid();
        var firstCommand = new CreateAppointmentCommand(
            doctorId,
            new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
            "Control anual",
            "key-1");
        await handler.Handle(firstCommand, CancellationToken.None);

        var differentCommand = new CreateAppointmentCommand(
            doctorId,
            new DateTimeOffset(2026, 7, 21, 15, 30, 0, TimeSpan.Zero),
            "Otro motivo",
            "key-1");

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(differentCommand, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithIdempotencyKeyOverMaxLength_ThrowsArgumentException()
    {
        var handler = CreateHandler(new AppointmentRepositoryStub(), hasConflict: false);
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
            "Control anual",
            new string('a', 201));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithIdempotencyKey_ConcurrentRaceLost_ReplaysWinnersResponseInsteadOfThrowing()
    {
        var appointments = new AppointmentRepositoryStub();
        var idempotencyStore = new FakeIdempotencyStore();
        var handler = CreateHandler(appointments, hasConflict: false, idempotencyStore: idempotencyStore);
        var command = new CreateAppointmentCommand(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
            "Control anual",
            "key-race");
        Guid winnerAppointmentId = idempotencyStore.SeedWinner(CurrentUserId, "key-race", command);

        // The handler still runs its own validation/Add/first SaveChanges before Stage()
        // discovers the race - this asserts the pipeline replays instead of throwing, not that
        // the appointment insert never happened locally (it did, and rolls back with the
        // transaction in the real Infrastructure implementation).
        CreateAppointmentResponse response = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(winnerAppointmentId, response.Id);
    }

    private static CreateAppointmentCommandHandler CreateHandler(
        AppointmentRepositoryStub appointments,
        bool hasConflict,
        FakeIdempotencyStore? idempotencyStore = null)
    {
        appointments.HasConflict = hasConflict;
        return new CreateAppointmentCommandHandler(
            new CurrentUserStub(),
            new ClockStub(),
            new ClinicScheduleStub(),
            new DoctorRepositoryStub(),
            appointments,
            idempotencyStore ?? new FakeIdempotencyStore(),
            new UnitOfWorkStub());
    }

    private sealed class CurrentUserStub : ICurrentUser
    {
        public Guid UserId => CurrentUserId;

        public UserRole Role => UserRole.Patient;
    }

    private sealed class ClockStub : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class ClinicScheduleStub : IClinicSchedule
    {
        public bool IsBookableSlot(DateTimeOffset startsAt) => true;

        public IReadOnlyList<DateTimeOffset> GetBookableSlots(DateOnly localDate) => [];
    }

    private sealed class DoctorRepositoryStub : IDoctorRepository
    {
        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public void Add(Doctor doctor)
        {
        }

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Doctor?>(null);

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
        {
        }
    }

    private sealed class AppointmentRepositoryStub : IAppointmentRepository
    {
        public bool HasConflict { get; set; }

        public Appointment? Added { get; set; }

        public Task<bool> HasScheduledAppointmentAsync(
            Guid doctorId,
            DateTimeOffset startsAt,
            Guid? excludeAppointmentId,
            CancellationToken cancellationToken) => Task.FromResult(HasConflict);

        public void Add(Appointment appointment) => Added = appointment;

        public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Appointment?>(null);

        public void PrepareStatusUpdate(Appointment appointment, uint version)
        {
        }

        public void PrepareRescheduleUpdate(Appointment appointment, uint version)
        {
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    // A genuinely-behaving fake (not a stub that throws the exception under test directly): it
    // tracks staged records itself and raises the same ConflictException a real unique-
    // constraint violation would, so the handler's own catch/retry/replay logic actually runs.
    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<(Guid UserId, string Operation, string Key), IdempotencyRecord> records = [];

        public bool HasRecordFor(Guid userId, string key) =>
            records.ContainsKey((userId, "CreateAppointment", key));

        // Seeds a record as if another concurrent request already committed it, so this
        // handler's own Stage() call collides - exactly like a real unique-constraint race.
        public Guid SeedWinner(Guid userId, string key, CreateAppointmentCommand command)
        {
            Guid winnerAppointmentId = Guid.NewGuid();
            var winnerResponse = new CreateAppointmentResponse(
                winnerAppointmentId,
                Guid.NewGuid(),
                command.DoctorId,
                command.StartsAt,
                command.StartsAt.AddMinutes(30),
                command.Reason,
                "SCHEDULED",
                "1");
            records[(userId, "CreateAppointment", key)] = new IdempotencyRecord(
                ComputeRequestHash(command),
                201,
                JsonSerializer.Serialize(winnerResponse));
            return winnerAppointmentId;
        }

        public Task<IdempotencyRecord?> FindAsync(
            Guid userId,
            string operation,
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(records.GetValueOrDefault((userId, operation, idempotencyKey)));

        public void Stage(
            Guid userId,
            string operation,
            string idempotencyKey,
            string requestHash,
            int responseStatus,
            string responseBody,
            DateTimeOffset expiresAt)
        {
            var key = (userId, operation, idempotencyKey);
            if (records.ContainsKey(key))
            {
                throw new ConflictException("The request conflicts with an existing record.");
            }

            records[key] = new IdempotencyRecord(requestHash, responseStatus, responseBody);
        }

        // Must mirror CreateAppointmentCommandHandler.ComputeRequestHash exactly so a seeded
        // "winner" record's hash matches what the handler independently computes.
        private static string ComputeRequestHash(CreateAppointmentCommand command)
        {
            string canonical = $"{command.DoctorId:D}|{command.StartsAt.UtcTicks}|{command.Reason.Trim()}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
