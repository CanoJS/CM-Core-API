using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MedicalAppointments.Api.OpenApi;

// Marks every operation as requiring the Bearer scheme in the OpenAPI document, except ones
// carrying [AllowAnonymous]/.AllowAnonymous() (e.g. GET /health/live) - mirrors the app's own
// auth pipeline (fallback policy requires an authenticated user unless AllowAnonymous is set),
// it does not change what the API actually enforces. References the scheme by id instead of
// registering it here: BearerSecuritySchemeTransformer (a document transformer, which runs after
// operation transformers) owns adding it to Components.SecuritySchemes; the reference resolves
// once the whole document is assembled.
public sealed class BearerSecurityRequirementOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        bool allowsAnonymous = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<AllowAnonymousAttribute>()
            .Any();
        if (allowsAnonymous)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, context.Document)] = [],
        });

        return Task.CompletedTask;
    }
}
