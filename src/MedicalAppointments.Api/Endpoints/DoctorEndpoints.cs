using MedicalAppointments.Application.Doctors.GetActiveDoctors;

namespace MedicalAppointments.Api.Endpoints;

public static class DoctorEndpoints
{
    public static IEndpointRouteBuilder MapDoctorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/doctors", GetActiveDoctors)
            .WithName("GetActiveDoctors")
            .WithTags("Doctors")
            .Produces<IReadOnlyList<DoctorResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> GetActiveDoctors(
        Guid? specialtyId,
        GetActiveDoctorsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DoctorResponse> response = await handler.Handle(
            new GetActiveDoctorsQuery(specialtyId),
            cancellationToken);

        return TypedResults.Ok(response);
    }
}
