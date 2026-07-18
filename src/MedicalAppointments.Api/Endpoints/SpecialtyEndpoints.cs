using MedicalAppointments.Application.Specialties.GetSpecialties;

namespace MedicalAppointments.Api.Endpoints;

public static class SpecialtyEndpoints
{
    public static IEndpointRouteBuilder MapSpecialtyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/specialties", GetSpecialties)
            .WithName("GetSpecialties")
            .WithTags("Specialties")
            .Produces<IReadOnlyList<SpecialtyResponse>>();

        return endpoints;
    }

    private static async Task<IResult> GetSpecialties(
        GetSpecialtiesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SpecialtyResponse> response = await handler.Handle(
            new GetSpecialtiesQuery(),
            cancellationToken);

        return TypedResults.Ok(response);
    }
}
