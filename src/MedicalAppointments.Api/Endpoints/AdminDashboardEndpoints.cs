using MedicalAppointments.Application.Admin.GetAdminDashboard;

namespace MedicalAppointments.Api.Endpoints;

public static class AdminDashboardEndpoints
{
    private const string AdminOnlyPolicy = "AdminOnly";

    public static IEndpointRouteBuilder MapAdminDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/dashboard", GetAdminDashboard)
            .WithName("GetAdminDashboard")
            .WithTags("AdminDashboard")
            .RequireAuthorization(AdminOnlyPolicy)
            .Produces<AdminDashboardResponse>();

        return endpoints;
    }

    private static async Task<IResult> GetAdminDashboard(
        GetAdminDashboardQueryHandler handler,
        CancellationToken cancellationToken)
    {
        AdminDashboardResponse response = await handler.Handle(new GetAdminDashboardQuery(), cancellationToken);
        return TypedResults.Ok(response);
    }
}
