using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Users.GetCurrentUser;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Users;

public sealed class GetCurrentUserQueryHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("003a9cee-6fc7-4218-b3f0-be99aab3b508");

    [Fact]
    public async Task Handle_WhenProfileIsActive_ReturnsCurrentProfile()
    {
        var profile = new UserProfileSnapshot(
            UserId,
            "Jesus",
            "Cano Mendez",
            "jesus@example.com",
            UserRole.Patient,
            true);
        var handler = CreateHandler(profile);

        CurrentUserResponse response = await handler.Handle(
            new GetCurrentUserQuery(),
            CancellationToken.None);

        Assert.Equal(UserId, response.Id);
        Assert.Equal("Jesus", response.FirstName);
        Assert.Equal("Cano Mendez", response.LastName);
        Assert.Equal("jesus@example.com", response.Email);
        Assert.Equal("PATIENT", response.Role);
        Assert.True(response.Active);
    }

    [Fact]
    public async Task Handle_WhenProfileDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(profile: null);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetCurrentUserQuery(),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenProfileIsInactive_ThrowsForbidden()
    {
        var profile = new UserProfileSnapshot(
            UserId,
            "Jesus",
            "Cano Mendez",
            "jesus@example.com",
            UserRole.Patient,
            false);
        var handler = CreateHandler(profile);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new GetCurrentUserQuery(),
            CancellationToken.None));
    }

    private static GetCurrentUserQueryHandler CreateHandler(UserProfileSnapshot? profile) =>
        new(new CurrentUserStub(), new UserProfileReaderStub(profile));

    private sealed class CurrentUserStub : ICurrentUser
    {
        public Guid UserId => GetCurrentUserQueryHandlerTests.UserId;

        public UserRole Role => UserRole.Patient;
    }

    private sealed class UserProfileReaderStub(UserProfileSnapshot? profile) : IUserProfileReader
    {
        public Task<UserProfileSnapshot?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken) => Task.FromResult(profile);
    }
}
