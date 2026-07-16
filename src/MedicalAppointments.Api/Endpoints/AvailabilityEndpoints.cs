using MedicalAppointments.Application.Availability.GetDoctorAvailability;

namespace MedicalAppointments.Api.Endpoints;

public static class AvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/doctors/{doctorId:guid}/availability", GetAvailability)
            .WithName("GetDoctorAvailability")
            .WithTags("Availability")
            .Produces<IReadOnlyList<DayAvailabilityResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetAvailability(
        Guid doctorId,
        DateOnly from,
        DateOnly to,
        GetDoctorAvailabilityQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetDoctorAvailabilityQuery(doctorId, from, to);
        IReadOnlyList<DayAvailabilityResponse> response = await handler.Handle(query, cancellationToken);
        return TypedResults.Ok(response);
    }
}
