using MedicalAppointments.Application.Abstractions.Auditing;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Appointments;
using MedicalAppointments.Application.Appointments.GetAppointmentById;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Appointments;

public sealed class GetAppointmentByIdQueryHandlerTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid DoctorUserId = Guid.NewGuid();
    private static readonly Guid DoctorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenAppointmentDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(UserRole.Admin, item: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetAppointmentByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenPatientOwnsAppointment_ReturnsResponse()
    {
        AppointmentListItem item = SampleItem(PatientId);
        var handler = CreateHandler(UserRole.Patient, item);

        AppointmentResponse response = await handler.Handle(
            new GetAppointmentByIdQuery(item.Id),
            CancellationToken.None);

        Assert.Equal(item.Id, response.Id);
    }

    [Fact]
    public async Task Handle_WhenPatientDoesNotOwnAppointment_ThrowsNotFound()
    {
        AppointmentListItem item = SampleItem(Guid.NewGuid());
        var handler = CreateHandler(UserRole.Patient, item);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetAppointmentByIdQuery(item.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorIsAssigned_ReturnsResponse()
    {
        AppointmentListItem item = SampleItem(PatientId);
        var handler = CreateHandler(UserRole.Doctor, item, doctorExists: true);

        AppointmentResponse response = await handler.Handle(
            new GetAppointmentByIdQuery(item.Id),
            CancellationToken.None);

        Assert.Equal(item.Id, response.Id);
    }

    [Fact]
    public async Task Handle_WhenDoctorIsNotAssigned_ThrowsNotFound()
    {
        AppointmentListItem item = SampleItem(PatientId) with { DoctorId = Guid.NewGuid() };
        var handler = CreateHandler(UserRole.Doctor, item, doctorExists: true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetAppointmentByIdQuery(item.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorProfileMissing_ThrowsNotFound()
    {
        AppointmentListItem item = SampleItem(PatientId);
        var handler = CreateHandler(UserRole.Doctor, item, doctorExists: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetAppointmentByIdQuery(item.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAdmin_ReturnsAnyAppointment()
    {
        AppointmentListItem item = SampleItem(Guid.NewGuid());
        var handler = CreateHandler(UserRole.Admin, item);

        AppointmentResponse response = await handler.Handle(
            new GetAppointmentByIdQuery(item.Id),
            CancellationToken.None);

        Assert.Equal(item.Id, response.Id);
    }

    [Fact]
    public async Task Handle_WhenPatient_DoesNotSeeMedicalNote()
    {
        AppointmentListItem item = SampleItem(PatientId) with
        {
            Status = AppointmentStatus.Attended,
            MedicalNote = "Diagnóstico confidencial.",
        };
        var handler = CreateHandler(UserRole.Patient, item);

        AppointmentResponse response = await handler.Handle(
            new GetAppointmentByIdQuery(item.Id),
            CancellationToken.None);

        Assert.Null(response.MedicalNote);
    }

    private static AppointmentListItem SampleItem(Guid patientId) => new(
        Guid.NewGuid(),
        patientId,
        "Ana",
        "López",
        DoctorId,
        "Carlos",
        "Ruiz",
        Guid.NewGuid(),
        "Cardiología",
        new DateTimeOffset(2026, 7, 20, 15, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
        AppointmentStatus.Scheduled,
        "Control anual",
        null,
        new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero),
        1);

    private static GetAppointmentByIdQueryHandler CreateHandler(
        UserRole role,
        AppointmentListItem? item,
        bool doctorExists = true) =>
        new(
            new CurrentUserStub(role),
            new DoctorRepositoryStub(doctorExists),
            new AppointmentReaderStub(item),
            new MedicalNoteAuditLogStub());

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = role == UserRole.Doctor ? DoctorUserId : PatientId;

        public UserRole Role => role;
    }

    private sealed class DoctorRepositoryStub(bool doctorExists) : IDoctorRepository
    {
        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public void Add(Doctor doctor)
        {
        }

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Doctor?>(null);

        public Task<Doctor?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(doctorExists ? new Doctor(DoctorId, DoctorUserId, Guid.NewGuid()) : null);

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
        {
        }
    }

    private sealed class AppointmentReaderStub(AppointmentListItem? item) : IAppointmentReader
    {
        public Task<IReadOnlyList<AppointmentListItem>> GetAsync(
            Guid? patientId,
            Guid? doctorId,
            AppointmentStatus? status,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtcExclusive,
            string? patientNameContains,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AppointmentListItem>>([]);

        public Task<AppointmentListItem?> GetByIdAsync(Guid appointmentId, CancellationToken cancellationToken) =>
            Task.FromResult(item is not null && item.Id == appointmentId ? item : null);
    }

    private sealed class MedicalNoteAuditLogStub : IMedicalNoteAuditLog
    {
        public void RecordRead(Guid appointmentId, Guid viewerUserId)
        {
        }
    }
}
