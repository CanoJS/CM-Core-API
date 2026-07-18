using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Specialties.GetAdminSpecialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Specialties;

public sealed class GetAdminSpecialtiesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenAdmin_ReturnsActiveAndInactiveSpecialties()
    {
        Guid activeId = Guid.NewGuid();
        Guid inactiveId = Guid.NewGuid();
        var handler = new GetAdminSpecialtiesQueryHandler(
            new CurrentUserStub(UserRole.Admin),
            new AdminSpecialtyReaderStub(
                new AdminSpecialtyItem(activeId, "Cardiología", true, 3),
                new AdminSpecialtyItem(inactiveId, "Pediatría", false, 7)));

        IReadOnlyList<AdminSpecialtyResponse> response = await handler.Handle(
            new GetAdminSpecialtiesQuery(),
            CancellationToken.None);

        Assert.Equal(2, response.Count);
        Assert.Contains(response, specialty => specialty.Id == activeId && specialty.Active && specialty.Version == "3");
        Assert.Contains(response, specialty => specialty.Id == inactiveId && !specialty.Active && specialty.Version == "7");
    }

    [Fact]
    public async Task Handle_WhenPatient_ThrowsForbidden()
    {
        var handler = new GetAdminSpecialtiesQueryHandler(
            new CurrentUserStub(UserRole.Patient),
            new AdminSpecialtyReaderStub());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetAdminSpecialtiesQuery(), CancellationToken.None));
    }

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class AdminSpecialtyReaderStub(params AdminSpecialtyItem[] items) : IAdminSpecialtyReader
    {
        public Task<IReadOnlyList<AdminSpecialtyItem>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminSpecialtyItem>>(items);
    }
}
