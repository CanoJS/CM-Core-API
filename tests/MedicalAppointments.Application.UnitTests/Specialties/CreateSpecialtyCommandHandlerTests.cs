using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Specialties.CreateSpecialty;
using MedicalAppointments.Domain.Common;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Specialties;

public sealed class CreateSpecialtyCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenAdminAndNameIsAvailable_CreatesSpecialty()
    {
        var repository = new SpecialtyRepositoryStub();
        var handler = CreateHandler(repository, UserRole.Admin);

        CreateSpecialtyResponse response = await handler.Handle(
            new CreateSpecialtyCommand("  Pediatría  "),
            CancellationToken.None);

        Assert.Equal("Pediatría", response.Name);
        Assert.True(response.Active);
        Assert.NotNull(repository.Added);
        Assert.Equal("0", response.Version);
    }

    [Fact]
    public async Task Handle_WhenPatient_ThrowsForbidden()
    {
        var handler = CreateHandler(new SpecialtyRepositoryStub(), UserRole.Patient);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new CreateSpecialtyCommand("Pediatría"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithEmptyName_ThrowsDomainException()
    {
        var handler = CreateHandler(new SpecialtyRepositoryStub(), UserRole.Admin);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new CreateSpecialtyCommand("   "), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNameLongerThan120Characters_ThrowsDomainException()
    {
        var handler = CreateHandler(new SpecialtyRepositoryStub(), UserRole.Admin);
        string longName = new string('a', 121);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new CreateSpecialtyCommand(longName), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenNameAlreadyExists_ThrowsConflict()
    {
        var repository = new SpecialtyRepositoryStub { NameExists = true };
        var handler = CreateHandler(repository, UserRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CreateSpecialtyCommand("Pediatría"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenNameExistsWithDifferentCase_ThrowsConflict()
    {
        var repository = new SpecialtyRepositoryStub { NameExists = true };
        var handler = CreateHandler(repository, UserRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CreateSpecialtyCommand("PEDIATRÍA"), CancellationToken.None));
    }

    private static CreateSpecialtyCommandHandler CreateHandler(
        SpecialtyRepositoryStub repository,
        UserRole role) =>
        new(new CurrentUserStub(role), repository, new UnitOfWorkStub());

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class SpecialtyRepositoryStub : ISpecialtyRepository
    {
        public bool NameExists { get; set; }

        public Specialty? Added { get; private set; }

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(NameExists);

        public void Add(Specialty specialty) => Added = specialty;

        public Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Specialty?>(null);

        public void SetVersion(Specialty specialty, uint version)
        {
        }
    }

    private sealed class UnitOfWorkStub : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
