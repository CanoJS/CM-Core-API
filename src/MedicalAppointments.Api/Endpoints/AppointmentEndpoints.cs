using MedicalAppointments.Application.Appointments.CreateAppointment;

namespace MedicalAppointments.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/appointments")
            .WithTags("Appointments");

        group.MapPost("/", CreateAppointment)
            .WithName("CreateAppointment")
            .Produces<CreateAppointmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> CreateAppointment(
        CreateAppointmentRequest request,
        CreateAppointmentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateAppointmentCommand(request.DoctorId, request.StartsAt, request.Reason);
        CreateAppointmentResponse response = await handler.Handle(command, cancellationToken);
        return TypedResults.Created($"/api/v1/appointments/{response.Id}", response);
    }

    private sealed record CreateAppointmentRequest(Guid DoctorId, DateTimeOffset StartsAt, string Reason);
}
