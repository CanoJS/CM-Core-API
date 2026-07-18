using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Doctors.ChangeDoctorStatus;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Doctors;

public sealed class ChangeDoctorStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAdminDeactivates_DeactivatesDoctorAndProfile()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var profile = new UserProfile(doctor.UserId, "Ana", "López", "ana@example.com", UserRole.Doctor);
        var doctorRepository = new DoctorRepositoryStub(doctor);
        var handler = CreateHandler(UserRole.Admin, doctorRepository, new UserProfileRepositoryStub(profile));

        ChangeDoctorStatusResponse response = await handler.Handle(
            new ChangeDoctorStatusCommand(doctor.Id, false, "0"),
            CancellationToken.None);

        Assert.False(response.Active);
        Assert.False(profile.Active);
        Assert.True(doctorRepository.PrepareStatusUpdateCalled);
    }

    [Fact]
    public async Task Handle_WhenAdminActivates_ActivatesDoctorAndProfile()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        doctor.SetActive(false);
        var profile = new UserProfile(doctor.UserId, "Ana", "López", "ana@example.com", UserRole.Doctor);
        profile.Deactivate();
        var handler = CreateHandler(
            UserRole.Admin,
            new DoctorRepositoryStub(doctor),
            new UserProfileRepositoryStub(profile));

        ChangeDoctorStatusResponse response = await handler.Handle(
            new ChangeDoctorStatusCommand(doctor.Id, true, "0"),
            CancellationToken.None);

        Assert.True(response.Active);
        Assert.True(profile.Active);
    }

    [Fact]
    public async Task Handle_WhenPatient_ThrowsForbidden()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var handler = CreateHandler(
            UserRole.Patient,
            new DoctorRepositoryStub(doctor),
            new UserProfileRepositoryStub(null));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new ChangeDoctorStatusCommand(doctor.Id, false, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(
            UserRole.Admin,
            new DoctorRepositoryStub(null),
            new UserProfileRepositoryStub(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ChangeDoctorStatusCommand(Guid.NewGuid(), false, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenVersionIsMalformed_ThrowsArgumentException()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var handler = CreateHandler(
            UserRole.Admin,
            new DoctorRepositoryStub(doctor),
            new UserProfileRepositoryStub(null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new ChangeDoctorStatusCommand(doctor.Id, false, "not-a-number"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenVersionIsStale_ThrowsConflict()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var profile = new UserProfile(doctor.UserId, "Ana", "López", "ana@example.com", UserRole.Doctor);
        var handler = CreateHandler(
            UserRole.Admin,
            new DoctorRepositoryStub(doctor),
            new UserProfileRepositoryStub(profile),
            unitOfWork: new UnitOfWorkStub(throwConflict: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new ChangeDoctorStatusCommand(doctor.Id, false, "0"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenVersionIsStaleAndDesiredStateMatchesCurrent_ThrowsConflict()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var profile = new UserProfile(doctor.UserId, "Ana", "López", "ana@example.com", UserRole.Doctor);
        var doctorRepository = new DoctorRepositoryStub(doctor);
        var handler = CreateHandler(
            UserRole.Admin,
            doctorRepository,
            new UserProfileRepositoryStub(profile),
            unitOfWork: new UnitOfWorkStub(throwConflict: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new ChangeDoctorStatusCommand(doctor.Id, true, "0"), CancellationToken.None));

        Assert.True(doctorRepository.PrepareStatusUpdateCalled);
    }

    private static ChangeDoctorStatusCommandHandler CreateHandler(
        UserRole role,
        DoctorRepositoryStub doctorRepository,
        UserProfileRepositoryStub userProfileRepository,
        UnitOfWorkStub? unitOfWork = null) =>
        new(
            new CurrentUserStub(role),
            doctorRepository,
            userProfileRepository,
            unitOfWork ?? new UnitOfWorkStub(throwConflict: false));

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class DoctorRepositoryStub(Doctor? doctor) : IDoctorRepository
    {
        public bool PrepareStatusUpdateCalled { get; private set; }

        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public void Add(Doctor doctor)
        {
        }

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(doctor);

        public void PrepareStatusUpdate(Doctor doctor, uint version) => PrepareStatusUpdateCalled = true;

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
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
