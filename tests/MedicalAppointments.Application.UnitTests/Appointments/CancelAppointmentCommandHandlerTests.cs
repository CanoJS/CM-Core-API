using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Appointments.CancelAppointment;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Common;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Appointments;

public sealed class CancelAppointmentCommandHandlerTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenDoctor_ThrowsForbidden()
    {
        var handler = CreateHandler(UserRole.Doctor, CreateAppointment(Now.AddDays(5)));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new CancelAppointmentCommand(Guid.NewGuid(), "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAppointmentDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(UserRole.Patient, appointment: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CancelAppointmentCommand(Guid.NewGuid(), "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenPatientCancelsSomeoneElsesAppointment_ThrowsNotFound()
    {
        Appointment appointment = CreateAppointment(Now.AddDays(5), patientId: Guid.NewGuid());
        var handler = CreateHandler(UserRole.Patient, appointment);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CancelAppointmentCommand(appointment.Id, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithMalformedVersion_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Patient, CreateAppointment(Now.AddDays(5)));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new CancelAppointmentCommand(Guid.NewGuid(), "not-a-number"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithStaleVersion_ThrowsConflict()
    {
        Appointment appointment = CreateAppointment(Now.AddDays(5));
        var handler = CreateHandler(UserRole.Admin, appointment, unitOfWork: new UnitOfWorkStub(throwConflict: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CancelAppointmentCommand(appointment.Id, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenPatientCancelsMoreThan24HoursAhead_Cancels()
    {
        Appointment appointment = CreateAppointment(Now.AddDays(2));
        var handler = CreateHandler(UserRole.Patient, appointment);

        CancelAppointmentResponse response = await handler.Handle(
            new CancelAppointmentCommand(appointment.Id, "0"),
            CancellationToken.None);

        Assert.Equal("CANCELLED", response.Status);
    }

    [Fact]
    public async Task Handle_WhenPatientCancelsWithinTwentyFourHours_ThrowsDomainException()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(2));
        var handler = CreateHandler(UserRole.Patient, appointment);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new CancelAppointmentCommand(appointment.Id, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAdminCancelsWithinTwentyFourHours_Cancels()
    {
        Appointment appointment = CreateAppointment(Now.AddHours(2));
        var handler = CreateHandler(UserRole.Admin, appointment);

        CancelAppointmentResponse response = await handler.Handle(
            new CancelAppointmentCommand(appointment.Id, "0"),
            CancellationToken.None);

        Assert.Equal("CANCELLED", response.Status);
    }

    [Fact]
    public async Task Handle_WhenAlreadyCancelled_ThrowsDomainException()
    {
        Appointment appointment = CreateAppointment(Now.AddDays(5));
        appointment.CancelByAdmin(Now);
        var handler = CreateHandler(UserRole.Admin, appointment);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new CancelAppointmentCommand(appointment.Id, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenPatientProfileIsInactive_ThrowsForbidden()
    {
        Appointment appointment = CreateAppointment(Now.AddDays(5));
        var handler = CreateHandler(
            UserRole.Patient,
            appointment,
            userProfileRepository: new UserProfileRepositoryStub(CreateInactivePatientProfile()));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new CancelAppointmentCommand(appointment.Id, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenPatientProfileIsMissing_ThrowsNotFound()
    {
        Appointment appointment = CreateAppointment(Now.AddDays(5));
        var handler = CreateHandler(
            UserRole.Patient,
            appointment,
            userProfileRepository: new UserProfileRepositoryStub(profile: null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CancelAppointmentCommand(appointment.Id, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAdminCancelsAndPatientProfileIsInactive_StillCancels()
    {
        // ADMIN's own behavior is unaffected by the patient's Active flag - only PATIENT-
        // initiated cancellations are gated by it.
        Appointment appointment = CreateAppointment(Now.AddDays(5));
        var handler = CreateHandler(
            UserRole.Admin,
            appointment,
            userProfileRepository: new UserProfileRepositoryStub(CreateInactivePatientProfile()));

        CancelAppointmentResponse response = await handler.Handle(
            new CancelAppointmentCommand(appointment.Id, "0"),
            CancellationToken.None);

        Assert.Equal("CANCELLED", response.Status);
    }

    private static Appointment CreateAppointment(DateTimeOffset startsAt, Guid? patientId = null) =>
        Appointment.Schedule(patientId ?? PatientId, Guid.NewGuid(), startsAt, "Control anual", Now);

    private static UserProfile CreateActivePatientProfile() =>
        new(PatientId, "Ana", "López", "ana@example.com", UserRole.Patient);

    private static UserProfile CreateInactivePatientProfile()
    {
        UserProfile profile = CreateActivePatientProfile();
        profile.Deactivate();
        return profile;
    }

    private static CancelAppointmentCommandHandler CreateHandler(
        UserRole role,
        Appointment? appointment,
        UnitOfWorkStub? unitOfWork = null,
        UserProfileRepositoryStub? userProfileRepository = null) =>
        new(
            new CurrentUserStub(role),
            new ClockStub(),
            new AppointmentRepositoryStub(appointment),
            userProfileRepository ?? new UserProfileRepositoryStub(CreateActivePatientProfile()),
            unitOfWork ?? new UnitOfWorkStub(throwConflict: false));

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = PatientId;

        public UserRole Role => role;
    }

    private sealed class ClockStub : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class AppointmentRepositoryStub(Appointment? appointment) : IAppointmentRepository
    {
        public bool PrepareStatusUpdateCalled { get; private set; }

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

        public void PrepareStatusUpdate(Appointment appointment, uint version) => PrepareStatusUpdateCalled = true;

        public void PrepareRescheduleUpdate(Appointment appointment, uint version)
        {
        }
    }

    private sealed class UserProfileRepositoryStub(UserProfile? profile) : IUserProfileRepository
    {
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(profile);
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
