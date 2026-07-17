using MedicalAppointments.Api.Authentication;
using MedicalAppointments.Api.Endpoints;
using MedicalAppointments.Api.ErrorHandling;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Appointments.CreateAppointment;
using MedicalAppointments.Application.Availability.GetDoctorAvailability;
using MedicalAppointments.Application.Doctors.GetActiveDoctors;
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

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<CreateAppointmentCommandHandler>();
builder.Services.AddScoped<GetDoctorAvailabilityQueryHandler>();
builder.Services.AddScoped<GetActiveDoctorsQueryHandler>();
builder.Services.AddScoped<GetSpecialtiesQueryHandler>();
builder.Services.AddScoped<GetCurrentUserQueryHandler>();
builder.Services.AddInfrastructure(connectionString, clinicTimeZone);

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

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithTags("Health");

app.MapAppointmentEndpoints();
app.MapAvailabilityEndpoints();
app.MapDoctorEndpoints();
app.MapSpecialtyEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program;
