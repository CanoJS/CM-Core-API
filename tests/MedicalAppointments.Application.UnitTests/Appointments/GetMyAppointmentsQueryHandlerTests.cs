using MedicalAppointments.Application.Abstractions.Auditing;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Appointments;
using MedicalAppointments.Application.Appointments.GetMyAppointments;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Appointments;

public sealed class GetMyAppointmentsQueryHandlerTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid DoctorUserId = Guid.NewGuid();
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly TimeZoneInfo ClinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    [Fact]
    public async Task Handle_WhenPatient_ScopesReaderToOwnPatientId()
    {
        var reader = new AppointmentReaderStub([SampleItem()]);
        var handler = CreateHandler(UserRole.Patient, reader);

        await handler.Handle(new GetMyAppointmentsQuery(null, null, null), CancellationToken.None);

        Assert.Equal(PatientId, reader.LastPatientId);
        Assert.Null(reader.LastDoctorId);
    }

    [Fact]
    public async Task Handle_WhenDoctor_ScopesReaderToOwnDoctorId()
    {
        var reader = new AppointmentReaderStub([SampleItem()]);
        var handler = CreateHandler(UserRole.Doctor, reader, doctorExists: true);

        await handler.Handle(new GetMyAppointmentsQuery(null, null, null), CancellationToken.None);

        Assert.Equal(DoctorId, reader.LastDoctorId);
        Assert.Null(reader.LastPatientId);
    }

    [Fact]
    public async Task Handle_WhenDoctorProfileMissing_ThrowsNotFound()
    {
        var handler = CreateHandler(UserRole.Doctor, new AppointmentReaderStub([]), doctorExists: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetMyAppointmentsQuery(null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAdmin_DoesNotScopeReader()
    {
        var reader = new AppointmentReaderStub([SampleItem()]);
        var handler = CreateHandler(UserRole.Admin, reader);

        await handler.Handle(new GetMyAppointmentsQuery(null, null, null), CancellationToken.None);

        Assert.Null(reader.LastPatientId);
        Assert.Null(reader.LastDoctorId);
    }

    [Fact]
    public async Task Handle_WithInvalidStatus_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Admin, new AppointmentReaderStub([]));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new GetMyAppointmentsQuery("NOT_A_STATUS", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithToBeforeFrom_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Admin, new AppointmentReaderStub([]));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new GetMyAppointmentsQuery(null, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 19)),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_With367InclusiveDays_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Admin, new AppointmentReaderStub([]));
        var from = new DateOnly(2026, 1, 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new GetMyAppointmentsQuery(null, from, from.AddDays(366)), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_With366InclusiveDays_DoesNotThrow()
    {
        var handler = CreateHandler(UserRole.Admin, new AppointmentReaderStub([]));
        var from = new DateOnly(2026, 1, 1);

        IReadOnlyList<AppointmentResponse> result = await handler.Handle(
            new GetMyAppointmentsQuery(null, from, from.AddDays(365)),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_MapsNamesAndSpecialtyIntoResponse()
    {
        var reader = new AppointmentReaderStub([SampleItem()]);
        var handler = CreateHandler(UserRole.Admin, reader);

        IReadOnlyList<AppointmentResponse> result = await handler.Handle(
            new GetMyAppointmentsQuery(null, null, null),
            CancellationToken.None);

        AppointmentResponse response = Assert.Single(result);
        Assert.Equal("Ana López", response.PatientName);
        Assert.Equal("Carlos Ruiz", response.DoctorName);
        Assert.Equal("Cardiología", response.SpecialtyName);
        Assert.Equal("SCHEDULED", response.Status);
    }

    [Fact]
    public async Task Handle_WhenPatient_MedicalNoteIsMaskedAndNotAudited()
    {
        var item = SampleItem() with { Status = AppointmentStatus.Attended, MedicalNote = "Diagnóstico confidencial." };
        var reader = new AppointmentReaderStub([item]);
        var auditLog = new MedicalNoteAuditLogStub();
        var handler = CreateHandler(UserRole.Patient, reader, auditLog: auditLog);

        IReadOnlyList<AppointmentResponse> result = await handler.Handle(
            new GetMyAppointmentsQuery(null, null, null),
            CancellationToken.None);

        Assert.Null(Assert.Single(result).MedicalNote);
        Assert.Empty(auditLog.ReadAppointmentIds);
    }

    [Fact]
    public async Task Handle_WhenDoctor_MedicalNoteIsVisibleAndAudited()
    {
        var item = SampleItem() with { Status = AppointmentStatus.Attended, MedicalNote = "Diagnóstico." };
        var reader = new AppointmentReaderStub([item]);
        var auditLog = new MedicalNoteAuditLogStub();
        var handler = CreateHandler(UserRole.Doctor, reader, doctorExists: true, auditLog: auditLog);

        IReadOnlyList<AppointmentResponse> result = await handler.Handle(
            new GetMyAppointmentsQuery(null, null, null),
            CancellationToken.None);

        Assert.Equal("Diagnóstico.", Assert.Single(result).MedicalNote);
        Assert.Equal(item.Id, Assert.Single(auditLog.ReadAppointmentIds));
    }

    private static AppointmentListItem SampleItem() => new(
        Guid.NewGuid(),
        PatientId,
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

    private static GetMyAppointmentsQueryHandler CreateHandler(
        UserRole role,
        AppointmentReaderStub reader,
        bool doctorExists = true,
        MedicalNoteAuditLogStub? auditLog = null) =>
        new(
            new CurrentUserStub(role),
            new DoctorRepositoryStub(doctorExists),
            reader,
            auditLog ?? new MedicalNoteAuditLogStub(),
            ClinicTimeZone);

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

    private sealed class AppointmentReaderStub(IReadOnlyList<AppointmentListItem> items) : IAppointmentReader
    {
        public Guid? LastPatientId { get; private set; }

        public Guid? LastDoctorId { get; private set; }

        public Task<IReadOnlyList<AppointmentListItem>> GetAsync(
            Guid? patientId,
            Guid? doctorId,
            AppointmentStatus? status,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtcExclusive,
            CancellationToken cancellationToken)
        {
            LastPatientId = patientId;
            LastDoctorId = doctorId;
            return Task.FromResult(items);
        }

        public Task<AppointmentListItem?> GetByIdAsync(Guid appointmentId, CancellationToken cancellationToken) =>
            Task.FromResult<AppointmentListItem?>(null);
    }

    private sealed class MedicalNoteAuditLogStub : IMedicalNoteAuditLog
    {
        public List<Guid> ReadAppointmentIds { get; } = [];

        public void RecordRead(Guid appointmentId, Guid viewerUserId) => ReadAppointmentIds.Add(appointmentId);
    }
}
