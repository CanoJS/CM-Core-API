using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Appointments.AttendAppointment;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Common;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Appointments;

public sealed class AttendAppointmentCommandHandlerTests
{
    private static readonly Guid DoctorUserId = Guid.NewGuid();
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserRole.Patient)]
    [InlineData(UserRole.Admin)]
    public async Task Handle_WhenNotDoctor_ThrowsForbidden(UserRole role)
    {
        var handler = CreateHandler(role, CreateAppointment(Now.AddHours(-1)), doctorExists: true);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(Guid.NewGuid(), "Nota.", "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorProfileIsMissing_ThrowsNotFound()
    {
        var handler = CreateHandler(UserRole.Doctor, appointment: null, doctorExists: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(Guid.NewGuid(), "Nota.", "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAppointmentDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(UserRole.Doctor, appointment: null, doctorExists: true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(Guid.NewGuid(), "Nota.", "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorProfileIsInactive_ThrowsForbidden()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(-1));
        var handler = CreateHandler(UserRole.Doctor, appointment, doctorExists: true, doctorActive: false);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(appointment.Id, "Nota.", "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAppointmentBelongsToAnotherDoctor_ThrowsNotFound()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(-1), doctorId: Guid.NewGuid());
        var handler = CreateHandler(UserRole.Doctor, appointment, doctorExists: true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(appointment.Id, "Nota.", "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithMalformedVersion_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Doctor, CreateAppointment(Now.AddHours(-1)), doctorExists: true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(Guid.NewGuid(), "Nota.", "not-a-number"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithStaleVersion_ThrowsConflict()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(-1));
        var handler = CreateHandler(
            UserRole.Doctor,
            appointment,
            doctorExists: true,
            unitOfWork: new UnitOfWorkStub(throwConflict: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(appointment.Id, "Nota.", "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithValidData_AttendsAppointment()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(-1));
        var handler = CreateHandler(UserRole.Doctor, appointment, doctorExists: true);

        AttendAppointmentResponse response = await handler.Handle(
            new AttendAppointmentCommand(appointment.Id, "Paciente estable.", "0"),
            CancellationToken.None);

        Assert.Equal("ATTENDED", response.Status);
        Assert.Equal("Paciente estable.", response.MedicalNote);
    }

    [Fact]
    public async Task Handle_BeforeStartsAt_ThrowsDomainException()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(1));
        var handler = CreateHandler(UserRole.Doctor, appointment, doctorExists: true);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(appointment.Id, "Paciente estable.", "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithoutMedicalNote_ThrowsDomainException()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(-1));
        var handler = CreateHandler(UserRole.Doctor, appointment, doctorExists: true);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(appointment.Id, "   ", "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAlreadyAttended_ThrowsDomainException()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(-1));
        appointment.Attend("Primera nota.", Now);
        var handler = CreateHandler(UserRole.Doctor, appointment, doctorExists: true);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new AttendAppointmentCommand(appointment.Id, "Segunda nota.", "0"),
                CancellationToken.None));
    }

    private static Appointment CreateAppointment(DateTimeOffset startsAt, Guid? doctorId = null) =>
        Appointment.Schedule(Guid.NewGuid(), doctorId ?? DoctorId, startsAt, "Control anual", Now.AddDays(-1));

    private static AttendAppointmentCommandHandler CreateHandler(
        UserRole role,
        Appointment? appointment,
        bool doctorExists,
        bool doctorActive = true,
        UnitOfWorkStub? unitOfWork = null) =>
        new(
            new CurrentUserStub(role),
            new ClockStub(),
            new DoctorRepositoryStub(doctorExists, doctorActive),
            new AppointmentRepositoryStub(appointment),
            unitOfWork ?? new UnitOfWorkStub(throwConflict: false));

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = DoctorUserId;

        public UserRole Role => role;
    }

    private sealed class ClockStub : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class DoctorRepositoryStub(bool doctorExists, bool doctorActive = true) : IDoctorRepository
    {
        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public void Add(Doctor doctor)
        {
        }

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Doctor?>(null);

        public Task<Doctor?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            if (!doctorExists)
            {
                return Task.FromResult<Doctor?>(null);
            }

            var doctor = new Doctor(DoctorId, DoctorUserId, Guid.NewGuid());
            if (!doctorActive)
            {
                doctor.SetActive(false);
            }

            return Task.FromResult<Doctor?>(doctor);
        }

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
        {
        }
    }

    private sealed class AppointmentRepositoryStub(Appointment? appointment) : IAppointmentRepository
    {
        public Task<bool> HasScheduledAppointmentAsync(
            Guid doctorId,
            DateTimeOffset startsAt,
            Guid? excludeAppointmentId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Appointment appointment)
        {
        }

        public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(appointment);

        public void PrepareStatusUpdate(Appointment appointment, uint version)
        {
        }

        public void PrepareRescheduleUpdate(Appointment appointment, uint version)
        {
        }
    }

    private sealed class UnitOfWorkStub(bool throwConflict) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (throwConflict)
            {
                throw new ConflictException("The resource was changed by another request. Refresh and try again.");
            }

            return Task.FromResult(1);
        }
    }
}
