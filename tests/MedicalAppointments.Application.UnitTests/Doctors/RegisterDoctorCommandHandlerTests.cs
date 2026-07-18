using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Doctors.RegisterDoctor;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Doctors;

public sealed class RegisterDoctorCommandHandlerTests
{
    private static readonly Guid SpecialtyId = Guid.NewGuid();
    private static readonly Guid InvitedUserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenAdminAndRequestIsValid_RegistersDoctor()
    {
        var doctorRepository = new DoctorRepositoryStub();
        var authAdminService = new AuthAdminServiceStub();
        var handler = CreateHandler(
            UserRole.Admin,
            doctorRepository: doctorRepository,
            authAdminService: authAdminService);

        RegisterDoctorResponse response = await handler.Handle(
            new RegisterDoctorCommand("  Ana  ", "  López  ", "  ANA@EXAMPLE.COM  ", SpecialtyId),
            CancellationToken.None);

        Assert.Equal("Ana", response.FirstName);
        Assert.Equal("ana@example.com", authAdminService.LastInviteEmail);
        Assert.NotNull(doctorRepository.Added);
        Assert.Equal(InvitedUserId, doctorRepository.Added.UserId);
        Assert.False(authAdminService.DeleteCalled);
    }

    [Fact]
    public async Task Handle_WhenPatient_ThrowsForbidden()
    {
        var handler = CreateHandler(UserRole.Patient);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));
    }

    [Theory]
    [InlineData("", "López")]
    [InlineData(" ", "López")]
    [InlineData("Ana", "")]
    public async Task Handle_WithMissingName_ThrowsArgumentException(string firstName, string lastName)
    {
        var handler = CreateHandler(UserRole.Admin);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new RegisterDoctorCommand(firstName, lastName, "ana@example.com", SpecialtyId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithInvalidEmailFormat_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Admin);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "not-an-email", SpecialtyId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithEmptySpecialtyId_ThrowsArgumentException()
    {
        var handler = CreateHandler(UserRole.Admin);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", Guid.Empty),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSpecialtyDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(UserRole.Admin, specialtyExists: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSpecialtyIsInactive_ThrowsConflict()
    {
        var specialty = new Specialty(SpecialtyId, "Pediatría");
        specialty.Deactivate();
        var handler = CreateHandler(UserRole.Admin, specialty: specialty);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ThrowsConflictWithoutInviting()
    {
        var authAdminService = new AuthAdminServiceStub();
        var handler = CreateHandler(
            UserRole.Admin,
            emailExists: true,
            authAdminService: authAdminService);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));

        Assert.False(authAdminService.InviteCalled);
    }

    [Fact]
    public async Task Handle_WhenAuthAdminServiceRejectsInvite_PropagatesWithoutCompensating()
    {
        var authAdminService = new AuthAdminServiceStub
        {
            InviteException = new AuthServiceException("The identity provider rejected the invitation."),
        };
        var handler = CreateHandler(UserRole.Admin, authAdminService: authAdminService);

        await Assert.ThrowsAsync<AuthServiceException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));

        Assert.False(authAdminService.DeleteCalled);
    }

    [Fact]
    public async Task Handle_WhenAuthAdminServiceIsUnavailable_PropagatesServiceUnavailable()
    {
        var authAdminService = new AuthAdminServiceStub
        {
            InviteException = new AuthServiceUnavailableException("The identity provider is not configured."),
        };
        var handler = CreateHandler(UserRole.Admin, authAdminService: authAdminService);

        await Assert.ThrowsAsync<AuthServiceUnavailableException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenPostgresFailsAfterInvite_CompensatesAndRethrowsOriginal()
    {
        var authAdminService = new AuthAdminServiceStub();
        var handler = CreateHandler(
            UserRole.Admin,
            authAdminService: authAdminService,
            unitOfWork: new UnitOfWorkStub(throwOnSave: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));

        Assert.True(authAdminService.DeleteCalled);
        Assert.Equal(InvitedUserId, authAdminService.DeletedUserId);
    }

    [Fact]
    public async Task Handle_WhenCompensationAlsoFails_StillRethrowsOriginalError()
    {
        var authAdminService = new AuthAdminServiceStub
        {
            DeleteException = new InvalidOperationException("boom"),
        };
        var handler = CreateHandler(
            UserRole.Admin,
            authAdminService: authAdminService,
            unitOfWork: new UnitOfWorkStub(throwOnSave: true));

        ConflictException exception = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));

        Assert.DoesNotContain("boom", exception.Message, StringComparison.Ordinal);
        Assert.True(authAdminService.DeleteCalled);
    }

    [Fact]
    public async Task Handle_WhenInvitedProfileWasNotProvisioned_CompensatesAndThrows()
    {
        var authAdminService = new AuthAdminServiceStub();
        var handler = CreateHandler(
            UserRole.Admin,
            authAdminService: authAdminService,
            userProfileRepository: new UserProfileRepositoryStub(profile: null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new RegisterDoctorCommand("Ana", "López", "ana@example.com", SpecialtyId),
                CancellationToken.None));

        Assert.True(authAdminService.DeleteCalled);
    }

    private static RegisterDoctorCommandHandler CreateHandler(
        UserRole role,
        Specialty? specialty = null,
        bool specialtyExists = true,
        bool emailExists = false,
        DoctorRepositoryStub? doctorRepository = null,
        UserProfileRepositoryStub? userProfileRepository = null,
        AuthAdminServiceStub? authAdminService = null,
        UnitOfWorkStub? unitOfWork = null)
    {
        specialty = specialtyExists ? specialty ?? new Specialty(SpecialtyId, "Pediatría") : null;
        return new RegisterDoctorCommandHandler(
            new CurrentUserStub(role),
            new SpecialtyRepositoryStub(specialty),
            userProfileRepository ?? new UserProfileRepositoryStub(
                profile: new UserProfile(InvitedUserId, "Ana", "López", "ana@example.com", UserRole.Patient),
                exists: emailExists),
            doctorRepository ?? new DoctorRepositoryStub(),
            authAdminService ?? new AuthAdminServiceStub(),
            unitOfWork ?? new UnitOfWorkStub(throwOnSave: false));
    }

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class SpecialtyRepositoryStub(Specialty? specialty) : ISpecialtyRepository
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
    }

    private sealed class UserProfileRepositoryStub(UserProfile? profile, bool exists = false)
        : IUserProfileRepository
    {
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(exists);

        public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(profile);
    }

    private sealed class DoctorRepositoryStub : IDoctorRepository
    {
        public Doctor? Added { get; private set; }

        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public void Add(Doctor doctor) => Added = doctor;

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Doctor?>(null);

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
        {
        }
    }

    private sealed class AuthAdminServiceStub : IAuthAdminService
    {
        public Exception? InviteException { get; set; }

        public Exception? DeleteException { get; set; }

        public bool InviteCalled { get; private set; }

        public bool DeleteCalled { get; private set; }

        public Guid? DeletedUserId { get; private set; }

        public string? LastInviteEmail { get; private set; }

        public Task<Guid> InviteDoctorAsync(
            string email,
            string firstName,
            string lastName,
            CancellationToken cancellationToken)
        {
            InviteCalled = true;
            LastInviteEmail = email;
            return InviteException is not null
                ? Task.FromException<Guid>(InviteException)
                : Task.FromResult(InvitedUserId);
        }

        public Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            DeleteCalled = true;
            DeletedUserId = userId;
            return DeleteException is not null ? Task.FromException(DeleteException) : Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkStub(bool throwOnSave) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (throwOnSave)
            {
                throw new ConflictException("The request conflicts with an existing record.");
            }

            return Task.FromResult(1);
        }
    }
}
