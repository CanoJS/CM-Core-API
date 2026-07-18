using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Specialties.ChangeSpecialtyStatus;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Specialties;

public sealed class ChangeSpecialtyStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAdminDeactivates_ReturnsUpdatedResponse()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Pediatría");
        var repository = new SpecialtyRepositoryStub(specialty);
        var handler = CreateHandler(repository, UserRole.Admin);

        ChangeSpecialtyStatusResponse response = await handler.Handle(
            new ChangeSpecialtyStatusCommand(specialty.Id, false, "0"),
            CancellationToken.None);

        Assert.False(response.Active);
        Assert.True(repository.VersionWasSet);
    }

    [Fact]
    public async Task Handle_WhenAdminActivates_ReturnsUpdatedResponse()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Pediatría");
        specialty.Deactivate();
        var repository = new SpecialtyRepositoryStub(specialty);
        var handler = CreateHandler(repository, UserRole.Admin);

        ChangeSpecialtyStatusResponse response = await handler.Handle(
            new ChangeSpecialtyStatusCommand(specialty.Id, true, "0"),
            CancellationToken.None);

        Assert.True(response.Active);
    }

    [Fact]
    public async Task Handle_WhenPatient_ThrowsForbidden()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Pediatría");
        var handler = CreateHandler(new SpecialtyRepositoryStub(specialty), UserRole.Patient);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new ChangeSpecialtyStatusCommand(specialty.Id, false, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSpecialtyDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(new SpecialtyRepositoryStub(null), UserRole.Admin);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new ChangeSpecialtyStatusCommand(Guid.NewGuid(), false, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenVersionIsStale_ThrowsConflict()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Pediatría");
        var repository = new SpecialtyRepositoryStub(specialty);
        var handler = CreateHandler(repository, UserRole.Admin, throwConflictOnSave: true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new ChangeSpecialtyStatusCommand(specialty.Id, false, "0"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenVersionIsMalformed_ThrowsArgumentException()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Pediatría");
        var handler = CreateHandler(new SpecialtyRepositoryStub(specialty), UserRole.Admin);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new ChangeSpecialtyStatusCommand(specialty.Id, false, "not-a-number"),
                CancellationToken.None));
    }

    private static ChangeSpecialtyStatusCommandHandler CreateHandler(
        SpecialtyRepositoryStub repository,
        UserRole role,
        bool throwConflictOnSave = false) =>
        new(new CurrentUserStub(role), repository, new UnitOfWorkStub(throwConflictOnSave));

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class SpecialtyRepositoryStub(Specialty? specialty) : ISpecialtyRepository
    {
        public bool VersionWasSet { get; private set; }

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Add(Specialty specialty)
        {
        }

        public Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(specialty);

        public void SetVersion(Specialty specialty, uint version) => VersionWasSet = true;
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
