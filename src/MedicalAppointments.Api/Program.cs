using MedicalAppointments.Api.Authentication;
using MedicalAppointments.Api.Endpoints;
using MedicalAppointments.Api.ErrorHandling;
using MedicalAppointments.Api.OpenApi;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Appointments.AttendAppointment;
using MedicalAppointments.Application.Appointments.CancelAppointment;
using MedicalAppointments.Application.Appointments.CreateAppointment;
using MedicalAppointments.Application.Appointments.GetAppointmentById;
using MedicalAppointments.Application.Appointments.GetMyAppointments;
using MedicalAppointments.Application.Appointments.RescheduleAppointment;
using MedicalAppointments.Application.Availability.GetDoctorAvailability;
using MedicalAppointments.Application.Doctors.ChangeDoctorSpecialty;
using MedicalAppointments.Application.Doctors.ChangeDoctorStatus;
using MedicalAppointments.Application.Doctors.GetActiveDoctors;
using MedicalAppointments.Application.Doctors.GetAdminDoctors;
using MedicalAppointments.Application.Doctors.RegisterDoctor;
using MedicalAppointments.Application.Specialties.ChangeSpecialtyStatus;
using MedicalAppointments.Application.Specialties.CreateSpecialty;
using MedicalAppointments.Application.Specialties.GetAdminSpecialties;
using MedicalAppointments.Application.Specialties.GetSpecialties;
using MedicalAppointments.Application.Users.GetCurrentUser;
using MedicalAppointments.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Connection string 'Database' is required.");
string projectUrl = builder.Configuration["Supabase:ProjectUrl"]
    ?? throw new InvalidOperationException("Supabase:ProjectUrl is required.");
string clinicTimeZone = builder.Configuration["Clinic:TimeZone"] ?? "America/Mexico_City";
string issuer = $"{projectUrl.TrimEnd('/')}/auth/v1";

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecurityRequirementOperationTransformer>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<CreateAppointmentCommandHandler>();
builder.Services.AddScoped<GetMyAppointmentsQueryHandler>();
builder.Services.AddScoped<GetAppointmentByIdQueryHandler>();
builder.Services.AddScoped<CancelAppointmentCommandHandler>();
builder.Services.AddScoped<RescheduleAppointmentCommandHandler>();
builder.Services.AddScoped<AttendAppointmentCommandHandler>();
builder.Services.AddScoped<GetDoctorAvailabilityQueryHandler>();
builder.Services.AddScoped<GetActiveDoctorsQueryHandler>();
builder.Services.AddScoped<GetSpecialtiesQueryHandler>();
builder.Services.AddScoped<GetCurrentUserQueryHandler>();
builder.Services.AddScoped<CreateSpecialtyCommandHandler>();
builder.Services.AddScoped<GetAdminSpecialtiesQueryHandler>();
builder.Services.AddScoped<ChangeSpecialtyStatusCommandHandler>();
builder.Services.AddScoped<RegisterDoctorCommandHandler>();
builder.Services.AddScoped<GetAdminDoctorsQueryHandler>();
builder.Services.AddScoped<ChangeDoctorStatusCommandHandler>();
builder.Services.AddScoped<ChangeDoctorSpecialtyCommandHandler>();
builder.Services.AddInfrastructure(connectionString, clinicTimeZone, projectUrl, builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = issuer;
        options.MetadataAddress = $"{issuer}/.well-known/openid-configuration";
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "email",
            RoleClaimType = "user_role",
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

const string AdminOnlyPolicy = "AdminOnly";

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(AdminOnlyPolicy, policy => policy.RequireRole("ADMIN"));

const string FrontendCorsPolicy = "FrontendCors";

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        // Read from builder.Configuration here, inside the policy delegate, rather than into a
        // local captured before this point: IOptions<CorsOptions> resolves this delegate lazily
        // (on first request), not synchronously at registration time. WebApplicationFactory-based
        // tests inject configuration overrides at builder.Build() time - a value snapshotted
        // earlier in Program.cs would miss those overrides, while this lazy read sees them.
        //
        // Cors:AllowAnyOrigin is a narrow, temporary opt-in for demo/MVP environments that need
        // quick Angular/Flutter integration without maintaining an allowlist yet. It must never
        // be combined with AllowCredentials() - browsers reject that combination, and this API
        // does not rely on cookies for auth (bearer tokens only), so there is nothing to protect
        // by pairing them anyway. Production deployments should set this to false (or omit it)
        // and use Cors:AllowedOrigins.
        bool allowAnyOrigin = builder.Configuration.GetValue<bool>("Cors:AllowAnyOrigin");
        if (allowAnyOrigin)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            string[] allowedOrigins =
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? [];
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

WebApplication app = builder.Build();

// The OpenAPI JSON document is always available in Development. In any other environment
// (e.g. a Production App Runner deploy) it stays off by default and requires an explicit,
// narrow opt-in via OpenApi:Enabled - not a full switch to Development, which would also
// enable detailed exception pages. See README "Habilitar OpenAPI temporalmente" for how to
// turn this on for a demo and back off afterward.
bool openApiEnabled = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("OpenApi:Enabled");
if (openApiEnabled)
{
    app.MapOpenApi().AllowAnonymous();

    // Swagger UI here is UI-only middleware (Swashbuckle.AspNetCore.SwaggerUI), not an endpoint,
    // so it is never subject to the fallback "authenticated user required" policy the way
    // MapOpenApi's endpoint is - registering it before UseAuthentication/UseAuthorization keeps
    // that true regardless of routing internals. It points at the /openapi/v1.json document
    // mapped above instead of generating its own: the project intentionally has no Swashbuckle
    // document generator, only Microsoft.AspNetCore.OpenApi, to avoid two competing sources of
    // the OpenAPI document.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "MedicalAppointments API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithTags("Health");

app.MapAppointmentEndpoints();
app.MapAvailabilityEndpoints();
app.MapDoctorEndpoints();
app.MapSpecialtyEndpoints();
app.MapAdminSpecialtyEndpoints();
app.MapAdminDoctorEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program;
