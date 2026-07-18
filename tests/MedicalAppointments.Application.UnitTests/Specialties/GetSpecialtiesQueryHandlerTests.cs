using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Specialties.GetSpecialties;

namespace MedicalAppointments.Application.UnitTests.Specialties;

public sealed class GetSpecialtiesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCatalogItems()
    {
        Guid specialtyId = Guid.NewGuid();
        var handler = new GetSpecialtiesQueryHandler(new SpecialtyCatalogReaderStub(
            new SpecialtyCatalogItem(specialtyId, "Cardiología")));

        IReadOnlyList<SpecialtyResponse> response = await handler.Handle(
            new GetSpecialtiesQuery(),
            CancellationToken.None);

        SpecialtyResponse specialty = Assert.Single(response);
        Assert.Equal(specialtyId, specialty.Id);
        Assert.Equal("Cardiología", specialty.Name);
    }

    private sealed class SpecialtyCatalogReaderStub(params SpecialtyCatalogItem[] specialties)
        : ISpecialtyCatalogReader
    {
        public Task<IReadOnlyList<SpecialtyCatalogItem>> GetActiveAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SpecialtyCatalogItem>>(specialties);
    }
}
