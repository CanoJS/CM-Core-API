using MedicalAppointments.Application.Users.GetCurrentUser;

namespace MedicalAppointments.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/users/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithTags("Users")
            .Produces<CurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetCurrentUser(
        GetCurrentUserQueryHandler handler,
        CancellationToken cancellationToken)
    {
        CurrentUserResponse response = await handler.Handle(
            new GetCurrentUserQuery(),
            cancellationToken);

        return TypedResults.Ok(response);
    }
}
