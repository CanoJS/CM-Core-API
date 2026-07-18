using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Doctors.GetAdminDoctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.UnitTests.Doctors;

public sealed class GetAdminDoctorsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenAdmin_ReturnsActiveAndInactiveDoctors()
    {
        Guid activeId = Guid.NewGuid();
        Guid inactiveId = Guid.NewGuid();
        Guid specialtyId = Guid.NewGuid();
        var handler = new GetAdminDoctorsQueryHandler(
            new CurrentUserStub(UserRole.Admin),
            new AdminDoctorReaderStub(
                new AdminDoctorItem(
                    activeId, Guid.NewGuid(), "Ana", "López", "ana@example.com",
                    specialtyId, "Pediatría", true, 1),
                new AdminDoctorItem(
                    inactiveId, Guid.NewGuid(), "Luis", "García", "luis@example.com",
                    specialtyId, "Pediatría", false, 4)));

        IReadOnlyList<AdminDoctorResponse> response = await handler.Handle(
            new GetAdminDoctorsQuery(),
            CancellationToken.None);

        Assert.Equal(2, response.Count);
        AdminDoctorResponse active = Assert.Single(response, doctor => doctor.Id == activeId);
        Assert.Equal("Ana López", active.FullName);
        Assert.Equal("1", active.Version);
        Assert.Contains(response, doctor => doctor.Id == inactiveId && !doctor.Active);
    }

    [Fact]
    public async Task Handle_WhenPatient_ThrowsForbidden()
    {
        var handler = new GetAdminDoctorsQueryHandler(
            new CurrentUserStub(UserRole.Patient),
            new AdminDoctorReaderStub());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetAdminDoctorsQuery(), CancellationToken.None));
    }

    private sealed class CurrentUserStub(UserRole role) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public UserRole Role => role;
    }

    private sealed class AdminDoctorReaderStub(params AdminDoctorItem[] items) : IAdminDoctorReader
    {
        public Task<IReadOnlyList<AdminDoctorItem>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminDoctorItem>>(items);
    }
}
