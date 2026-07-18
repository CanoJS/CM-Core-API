using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Doctors.ChangeDoctorSpecialty;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Doctors;

public sealed class ChangeDoctorSpecialtyCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAdminAndSpecialtyIsActive_ChangesSpecialty()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var newSpecialty = new Specialty(Guid.NewGuid(), "Cardiología");
        var doctorRepository = new DoctorRepositoryStub(doctor);
        var handler = CreateHandler(UserRole.Admin, doctorRepository, new SpecialtyRepositoryStub(newSpecialty));

        ChangeDoctorSpecialtyResponse response = await handler.Handle(
            new ChangeDoctorSpecialtyCommand(doctor.Id, newSpecialty.Id, "0"),
            CancellationToken.None);

        Assert.Equal(newSpecialty.Id, response.Specialty.Id);
        Assert.Equal(newSpecialty.Id, doctor.SpecialtyId);
        Assert.True(doctorRepository.PrepareSpecialtyUpdateCalled);
    }

    [Fact]
    public async Task Handle_WhenPatient_ThrowsForbidden()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var specialty = new Specialty(Guid.NewGuid(), "Cardiología");
        var handler = CreateHandler(UserRole.Patient, new DoctorRepositoryStub(doctor), new SpecialtyRepositoryStub(specialty));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new ChangeDoctorSpecialtyCommand(doctor.Id, specialty.Id, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSpecialtyDoesNotExist_ThrowsNotFound()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var handler = CreateHandler(UserRole.Admin, new DoctorRepositoryStub(doctor), new SpecialtyRepositoryStub(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new ChangeDoctorSpecialtyCommand(doctor.Id, Guid.NewGuid(), "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSpecialtyIsInactive_ThrowsConflict()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var specialty = new Specialty(Guid.NewGuid(), "Cardiología");
        specialty.Deactivate();
        var handler = CreateHandler(UserRole.Admin, new DoctorRepositoryStub(doctor), new SpecialtyRepositoryStub(specialty));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new ChangeDoctorSpecialtyCommand(doctor.Id, specialty.Id, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorDoesNotExist_ThrowsNotFound()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Cardiología");
        var handler = CreateHandler(UserRole.Admin, new DoctorRepositoryStub(null), new SpecialtyRepositoryStub(specialty));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new ChangeDoctorSpecialtyCommand(Guid.NewGuid(), specialty.Id, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenVersionIsMalformed_ThrowsArgumentException()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var specialty = new Specialty(Guid.NewGuid(), "Cardiología");
        var handler = CreateHandler(UserRole.Admin, new DoctorRepositoryStub(doctor), new SpecialtyRepositoryStub(specialty));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new ChangeDoctorSpecialtyCommand(doctor.Id, specialty.Id, "not-a-number"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenVersionIsStale_ThrowsConflict()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var specialty = new Specialty(Guid.NewGuid(), "Cardiología");
        var handler = CreateHandler(
            UserRole.Admin,
            new DoctorRepositoryStub(doctor),
            new SpecialtyRepositoryStub(specialty),
            unitOfWork: new UnitOfWorkStub(throwConflict: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new ChangeDoctorSpecialtyCommand(doctor.Id, specialty.Id, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSpecialtyBecameInactiveAfterInitialCheck_ThrowsConflict()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var initialSpecialty = new Specialty(Guid.NewGuid(), "Cardiología");
        var lockedSpecialty = new Specialty(initialSpecialty.Id, "Cardiología");
        lockedSpecialty.Deactivate();
        var doctorRepository = new DoctorRepositoryStub(doctor);
        var handler = CreateHandler(
            UserRole.Admin,
            doctorRepository,
            new SpecialtyRepositoryStub(initialSpecialty, lockedSpecialty));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new ChangeDoctorSpecialtyCommand(doctor.Id, initialSpecialty.Id, "0"),
                CancellationToken.None));

        Assert.False(doctorRepository.PrepareSpecialtyUpdateCalled);
    }

    [Fact]
    public async Task Handle_WhenVersionIsStaleAndSpecialtyMatchesCurrent_ThrowsConflict()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Cardiología");
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), specialty.Id);
        var doctorRepository = new DoctorRepositoryStub(doctor);
        var handler = CreateHandler(
            UserRole.Admin,
            doctorRepository,
            new SpecialtyRepositoryStub(specialty),
            unitOfWork: new UnitOfWorkStub(throwConflict: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new ChangeDoctorSpecialtyCommand(doctor.Id, specialty.Id, "0"),
                CancellationToken.None));

        Assert.True(doctorRepository.PrepareSpecialtyUpdateCalled);
    }

    private static ChangeDoctorSpecialtyCommandHandler CreateHandler(
        UserRole role,
        DoctorRepositoryStub doctorRepository,
        SpecialtyRepositoryStub specialtyRepository,
        UnitOfWorkStub? unitOfWork = null) =>
        new(
            new CurrentUserStub(role),
            doctorRepository,
            specialtyRepository,
            unitOfWork ?? new UnitOfWorkStub(throwConflict: false));

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class DoctorRepositoryStub(Doctor? doctor) : IDoctorRepository
    {
        public bool PrepareSpecialtyUpdateCalled { get; private set; }

        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public void Add(Doctor doctor)
        {
        }

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(doctor);

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version) => PrepareSpecialtyUpdateCalled = true;
    }

    private sealed class SpecialtyRepositoryStub(Specialty? specialty, Specialty? lockedSpecialty = null)
        : ISpecialtyRepository
    {
        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Add(Specialty specialty)
        {
        }

        public Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(specialty);

        public void PrepareStatusUpdate(Specialty specialty, uint version)
        {
        }

        public Task<Specialty?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(lockedSpecialty ?? specialty);
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
