using MedicalAppointments.Application.Specialties.ChangeSpecialtyStatus;
using MedicalAppointments.Application.Specialties.CreateSpecialty;
using MedicalAppointments.Application.Specialties.GetAdminSpecialties;

namespace MedicalAppointments.Api.Endpoints;

public static class AdminSpecialtyEndpoints
{
    private const string AdminOnlyPolicy = "AdminOnly";

    public static IEndpointRouteBuilder MapAdminSpecialtyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/admin/specialties")
            .WithTags("AdminSpecialties")
            .RequireAuthorization(AdminOnlyPolicy);

        group.MapPost("/", CreateSpecialty)
            .WithName("CreateSpecialty")
            .Produces<CreateSpecialtyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", GetAdminSpecialties)
            .WithName("GetAdminSpecialties")
            .Produces<IReadOnlyList<AdminSpecialtyResponse>>();

        group.MapPatch("/{id:guid}/status", ChangeSpecialtyStatus)
            .WithName("ChangeSpecialtyStatus")
            .Produces<ChangeSpecialtyStatusResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> CreateSpecialty(
        CreateSpecialtyRequest request,
        CreateSpecialtyCommandHandler handler,
        CancellationToken cancellationToken)
    {
        CreateSpecialtyResponse response = await handler.Handle(
            new CreateSpecialtyCommand(request.Name),
            cancellationToken);

        return TypedResults.Created($"/api/v1/admin/specialties/{response.Id}", response);
    }

    private static async Task<IResult> GetAdminSpecialties(
        GetAdminSpecialtiesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminSpecialtyResponse> response = await handler.Handle(
            new GetAdminSpecialtiesQuery(),
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ChangeSpecialtyStatus(
        Guid id,
        ChangeSpecialtyStatusRequest request,
        ChangeSpecialtyStatusCommandHandler handler,
        CancellationToken cancellationToken)
    {
        ChangeSpecialtyStatusResponse response = await handler.Handle(
            new ChangeSpecialtyStatusCommand(id, request.Active, request.Version),
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private sealed record CreateSpecialtyRequest(string Name);

    private sealed record ChangeSpecialtyStatusRequest(bool Active, string Version);
}
