using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Application.Appointments.RescheduleAppointment;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Common;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Appointments;

public sealed class RescheduleAppointmentCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NewStartsAt = new(2026, 7, 21, 16, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserRole.Patient)]
    [InlineData(UserRole.Doctor)]
    public async Task Handle_WhenNotAdmin_ThrowsForbidden(UserRole role)
    {
        var handler = CreateHandler(role, CreateAppointment());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(Guid.NewGuid(), Guid.NewGuid(), NewStartsAt, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAppointmentDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(UserRole.Admin, appointment: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(Guid.NewGuid(), Guid.NewGuid(), NewStartsAt, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorDoesNotExistOrInactive_ThrowsNotFound()
    {
        Appointment appointment = CreateAppointment();
        var handler = CreateHandler(UserRole.Admin, appointment, doctorActive: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(appointment.Id, Guid.NewGuid(), NewStartsAt, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithMalformedVersion_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Admin, CreateAppointment());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(Guid.NewGuid(), Guid.NewGuid(), NewStartsAt, "not-a-number"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithStaleVersion_ThrowsConflict()
    {
        Appointment appointment = CreateAppointment();
        var handler = CreateHandler(UserRole.Admin, appointment, unitOfWork: new UnitOfWorkStub(throwConflict: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(appointment.Id, Guid.NewGuid(), NewStartsAt, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNonBookableSlot_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Admin, CreateAppointment(), isBookableSlot: false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(Guid.NewGuid(), Guid.NewGuid(), NewStartsAt, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSlotIsOccupied_ThrowsConflict()
    {
        Appointment appointment = CreateAppointment();
        var handler = CreateHandler(UserRole.Admin, appointment, slotOccupied: true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(appointment.Id, Guid.NewGuid(), NewStartsAt, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithValidData_ReschedulesDoctorAndStartsAt()
    {
        Appointment appointment = CreateAppointment();
        Guid newDoctorId = Guid.NewGuid();
        var handler = CreateHandler(UserRole.Admin, appointment);

        RescheduleAppointmentResponse response = await handler.Handle(
            new RescheduleAppointmentCommand(appointment.Id, newDoctorId, NewStartsAt, "0"),
            CancellationToken.None);

        Assert.Equal(newDoctorId, response.DoctorId);
        Assert.Equal(NewStartsAt, response.StartsAt);
    }

    [Fact]
    public async Task Handle_WithSameDoctorAndTime_StillForcesUpdateAndDetectsStaleVersion()
    {
        Appointment appointment = CreateAppointment();
        var appointments = new AppointmentRepositoryStub(appointment, slotOccupied: false);
        var handler = CreateHandler(
            UserRole.Admin,
            appointment,
            unitOfWork: new UnitOfWorkStub(throwConflict: true),
            appointmentRepository: appointments);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(appointment.Id, appointment.DoctorId, appointment.StartsAt, "0"),
                CancellationToken.None));

        Assert.True(appointments.PrepareRescheduleUpdateCalled);
    }

    [Fact]
    public async Task Handle_WhenAlreadyCancelled_ThrowsDomainException()
    {
        Appointment appointment = CreateAppointment();
        appointment.CancelByAdmin(Now);
        var handler = CreateHandler(UserRole.Admin, appointment);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(
                new RescheduleAppointmentCommand(appointment.Id, Guid.NewGuid(), NewStartsAt, "0"),
                CancellationToken.None));
    }

    private static Appointment CreateAppointment() =>
        Appointment.Schedule(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(1), "Control anual", Now);

    private static RescheduleAppointmentCommandHandler CreateHandler(
        UserRole role,
        Appointment? appointment,
        bool doctorActive = true,
        bool slotOccupied = false,
        bool isBookableSlot = true,
        UnitOfWorkStub? unitOfWork = null,
        AppointmentRepositoryStub? appointmentRepository = null) =>
        new(
            new CurrentUserStub(role),
            new ClockStub(),
            new ClinicScheduleStub(isBookableSlot),
            new DoctorRepositoryStub(doctorActive),
            appointmentRepository ?? new AppointmentRepositoryStub(appointment, slotOccupied),
            unitOfWork ?? new UnitOfWorkStub(throwConflict: false));

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class ClockStub : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class ClinicScheduleStub(bool isBookableSlot) : IClinicSchedule
    {
        public bool IsBookableSlot(DateTimeOffset startsAt) => isBookableSlot;

        public IReadOnlyList<DateTimeOffset> GetBookableSlots(DateOnly localDate) => [];
    }

    private sealed class DoctorRepositoryStub(bool isActive) : IDoctorRepository
    {
        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(isActive);

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

    private sealed class AppointmentRepositoryStub(Appointment? appointment, bool slotOccupied = false)
        : IAppointmentRepository
    {
        public bool PrepareRescheduleUpdateCalled { get; private set; }

        public Task<bool> HasScheduledAppointmentAsync(
            Guid doctorId,
            DateTimeOffset startsAt,
            Guid? excludeAppointmentId,
            CancellationToken cancellationToken) => Task.FromResult(slotOccupied);

        public void Add(Appointment appointment)
        {
        }

        public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(appointment);

        public void PrepareStatusUpdate(Appointment appointment, uint version)
        {
        }

        public void PrepareRescheduleUpdate(Appointment appointment, uint version) =>
            PrepareRescheduleUpdateCalled = true;
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
