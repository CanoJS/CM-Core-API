using MedicalAppointments.Application.Appointments;
using MedicalAppointments.Application.Appointments.AttendAppointment;
using MedicalAppointments.Application.Appointments.CancelAppointment;
using MedicalAppointments.Application.Appointments.CreateAppointment;
using MedicalAppointments.Application.Appointments.GetAppointmentById;
using MedicalAppointments.Application.Appointments.GetMyAppointments;
using MedicalAppointments.Application.Appointments.RescheduleAppointment;
using Microsoft.AspNetCore.Mvc;

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

        group.MapGet("/", GetMyAppointments)
            .WithName("GetMyAppointments")
            .Produces<IReadOnlyList<AppointmentResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetAppointmentById)
            .WithName("GetAppointmentById")
            .Produces<AppointmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/cancel", CancelAppointment)
            .WithName("CancelAppointment")
            .Produces<CancelAppointmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/reschedule", RescheduleAppointment)
            .WithName("RescheduleAppointment")
            .Produces<RescheduleAppointmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/attend", AttendAppointment)
            .WithName("AttendAppointment")
            .Produces<AttendAppointmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> CreateAppointment(
        CreateAppointmentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CreateAppointmentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateAppointmentCommand(request.DoctorId, request.StartsAt, request.Reason, idempotencyKey);
        CreateAppointmentResponse response = await handler.Handle(command, cancellationToken);
        return TypedResults.Created($"/api/v1/appointments/{response.Id}", response);
    }

    private static async Task<IResult> GetMyAppointments(
        string? status,
        DateOnly? from,
        DateOnly? to,
        GetMyAppointmentsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetMyAppointmentsQuery(status, from, to);
        IReadOnlyList<AppointmentResponse> response = await handler.Handle(query, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetAppointmentById(
        Guid id,
        GetAppointmentByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        AppointmentResponse response = await handler.Handle(new GetAppointmentByIdQuery(id), cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> CancelAppointment(
        Guid id,
        CancelAppointmentRequest request,
        CancelAppointmentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CancelAppointmentCommand(id, request.Version);
        CancelAppointmentResponse response = await handler.Handle(command, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> RescheduleAppointment(
        Guid id,
        RescheduleAppointmentRequest request,
        RescheduleAppointmentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RescheduleAppointmentCommand(id, request.DoctorId, request.StartsAt, request.Version);
        RescheduleAppointmentResponse response = await handler.Handle(command, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> AttendAppointment(
        Guid id,
        AttendAppointmentRequest request,
        AttendAppointmentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new AttendAppointmentCommand(id, request.MedicalNote, request.Version);
        AttendAppointmentResponse response = await handler.Handle(command, cancellationToken);
        return TypedResults.Ok(response);
    }

    private sealed record CreateAppointmentRequest(Guid DoctorId, DateTimeOffset StartsAt, string Reason);

    private sealed record CancelAppointmentRequest(string Version);

    private sealed record RescheduleAppointmentRequest(Guid DoctorId, DateTimeOffset StartsAt, string Version);

    private sealed record AttendAppointmentRequest(string MedicalNote, string Version);
}
