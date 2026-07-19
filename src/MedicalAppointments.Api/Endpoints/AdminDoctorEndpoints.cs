using MedicalAppointments.Application.Doctors.ChangeDoctorSpecialty;
using MedicalAppointments.Application.Doctors.ChangeDoctorStatus;
using MedicalAppointments.Application.Doctors.GetAdminDoctors;
using MedicalAppointments.Application.Doctors.RegisterDoctor;

namespace MedicalAppointments.Api.Endpoints;

public static class AdminDoctorEndpoints
{
    private const string AdminOnlyPolicy = "AdminOnly";

    public static IEndpointRouteBuilder MapAdminDoctorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/admin/doctors")
            .WithTags("AdminDoctors")
            .RequireAuthorization(AdminOnlyPolicy);

        group.MapPost("/", RegisterDoctor)
            .WithName("RegisterDoctor")
            .Produces<RegisterDoctorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/", GetAdminDoctors)
            .WithName("GetAdminDoctors")
            .Produces<IReadOnlyList<AdminDoctorResponse>>();

        group.MapPatch("/{id:guid}/status", ChangeDoctorStatus)
            .WithName("ChangeDoctorStatus")
            .Produces<ChangeDoctorStatusResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/specialty", ChangeDoctorSpecialty)
            .WithName("ChangeDoctorSpecialty")
            .Produces<ChangeDoctorSpecialtyResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> RegisterDoctor(
        RegisterDoctorRequest request,
        RegisterDoctorCommandHandler handler,
        CancellationToken cancellationToken)
    {
        RegisterDoctorResponse response = await handler.Handle(
            new RegisterDoctorCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.SpecialtyId,
                request.TemporaryPassword),
            cancellationToken);

        return TypedResults.Created($"/api/v1/admin/doctors/{response.Id}", response);
    }

    private static async Task<IResult> GetAdminDoctors(
        GetAdminDoctorsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminDoctorResponse> response = await handler.Handle(
            new GetAdminDoctorsQuery(),
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ChangeDoctorStatus(
        Guid id,
        ChangeDoctorStatusRequest request,
        ChangeDoctorStatusCommandHandler handler,
        CancellationToken cancellationToken)
    {
        ChangeDoctorStatusResponse response = await handler.Handle(
            new ChangeDoctorStatusCommand(id, request.Active, request.Version),
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ChangeDoctorSpecialty(
        Guid id,
        ChangeDoctorSpecialtyRequest request,
        ChangeDoctorSpecialtyCommandHandler handler,
        CancellationToken cancellationToken)
    {
        ChangeDoctorSpecialtyResponse response = await handler.Handle(
            new ChangeDoctorSpecialtyCommand(id, request.SpecialtyId, request.Version),
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private sealed record RegisterDoctorRequest(
        string FirstName,
        string LastName,
        string Email,
        Guid SpecialtyId,
        string TemporaryPassword);

    private sealed record ChangeDoctorStatusRequest(bool Active, string Version);

    private sealed record ChangeDoctorSpecialtyRequest(Guid SpecialtyId, string Version);
}
