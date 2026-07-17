using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Doctors.GetActiveDoctors;

namespace MedicalAppointments.Application.UnitTests.Doctors;

public sealed class GetActiveDoctorsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFrontendContractAndPassesFilter()
    {
        Guid doctorId = Guid.NewGuid();
        Guid specialtyId = Guid.NewGuid();
        var reader = new DoctorCatalogReaderStub(new DoctorCatalogItem(
            doctorId,
            "Ana",
            "López",
            "ana@example.com",
            specialtyId,
            "Pediatría",
            true));
        var handler = new GetActiveDoctorsQueryHandler(reader);

        IReadOnlyList<DoctorResponse> response = await handler.Handle(
            new GetActiveDoctorsQuery(specialtyId),
            CancellationToken.None);

        DoctorResponse doctor = Assert.Single(response);
        Assert.Equal(specialtyId, reader.ReceivedSpecialtyId);
        Assert.Equal(doctorId, doctor.Id);
        Assert.Equal("Ana López", doctor.FullName);
        Assert.Equal("ana@example.com", doctor.Email);
        Assert.Equal(specialtyId, doctor.Specialty.Id);
        Assert.Equal("Pediatría", doctor.Specialty.Name);
        Assert.True(doctor.Active);
    }

    [Fact]
    public async Task Handle_WithEmptySpecialtyIdentifier_ThrowsArgumentException()
    {
        var handler = new GetActiveDoctorsQueryHandler(new DoctorCatalogReaderStub());

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new GetActiveDoctorsQuery(Guid.Empty),
            CancellationToken.None));
    }

    private sealed class DoctorCatalogReaderStub(params DoctorCatalogItem[] doctors)
        : IDoctorCatalogReader
    {
        public Guid? ReceivedSpecialtyId { get; private set; }

        public Task<IReadOnlyList<DoctorCatalogItem>> GetActiveAsync(
            Guid? specialtyId,
            CancellationToken cancellationToken)
        {
            ReceivedSpecialtyId = specialtyId;
            return Task.FromResult<IReadOnlyList<DoctorCatalogItem>>(doctors);
        }
    }
}
