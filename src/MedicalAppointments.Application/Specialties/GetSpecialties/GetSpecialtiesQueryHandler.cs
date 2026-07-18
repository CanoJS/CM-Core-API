using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Queries;

namespace MedicalAppointments.Application.Specialties.GetSpecialties;

public sealed class GetSpecialtiesQueryHandler(ISpecialtyCatalogReader specialtyCatalogReader)
    : IQueryHandler<GetSpecialtiesQuery, IReadOnlyList<SpecialtyResponse>>
{
    public async Task<IReadOnlyList<SpecialtyResponse>> Handle(
        GetSpecialtiesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SpecialtyCatalogItem> specialties =
            await specialtyCatalogReader.GetActiveAsync(cancellationToken);

        return specialties
            .Select(specialty => new SpecialtyResponse(specialty.Id, specialty.Name))
            .ToArray();
    }
}
