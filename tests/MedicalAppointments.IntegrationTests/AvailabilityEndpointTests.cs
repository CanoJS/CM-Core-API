using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Domain.Doctors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAppointments.IntegrationTests;

public sealed class AvailabilityEndpointTests
{
    private static readonly Guid DoctorId = Guid.NewGuid();

    [Fact]
    public async Task GetAvailability_WithoutAccessToken_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/doctors/{DoctorId}/availability?from=2026-07-20&to=2026-07-20",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_WithValidToken_ReturnsOkWithExpectedJsonShape()
    {
        using HttpClient client = CreateAuthenticatedClient(doctorExists: true);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/doctors/{DoctorId}/availability?from=2026-07-20&to=2026-07-20",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement day = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("2026-07-20", day.GetProperty("date").GetString());
        JsonElement[] slots = [.. day.GetProperty("slots").EnumerateArray()];
        Assert.Equal(20, slots.Length);
        Assert.True(slots[0].TryGetProperty("startsAt", out _));
        Assert.True(slots[0].TryGetProperty("available", out JsonElement available));
        Assert.True(available.ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.False(slots[0].TryGetProperty("endsAt", out _));
    }

    [Theory]
    [InlineData("2026-07-21", "2026-07-20")]
    public async Task GetAvailability_WithToBeforeFrom_ReturnsBadRequest(string from, string to)
    {
        using HttpClient client = CreateAuthenticatedClient(doctorExists: true);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/doctors/{DoctorId}/availability?from={from}&to={to}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_With32InclusiveDays_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient(doctorExists: true);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/doctors/{DoctorId}/availability?from=2026-07-20&to=2026-08-20",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_WhenDoctorDoesNotExistOrIsInactive_ReturnsNotFound()
    {
        using HttpClient client = CreateAuthenticatedClient(doctorExists: false);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/doctors/{DoctorId}/availability?from=2026-07-20&to=2026-07-20",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_WithoutFromOrTo_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient(doctorExists: true);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/doctors/{DoctorId}/availability",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_WithMalformedDateFormat_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient(doctorExists: true);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/doctors/{DoctorId}/availability?from=not-a-date&to=2026-07-20",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(bool doctorExists)
    {
        var factory = new WebApplicationFactory<Program>();
        WebApplicationFactory<Program> testFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
                    })
                    .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.Scheme,
                        options => options.Role = "PATIENT");

                services.RemoveAll<IDoctorRepository>();
                services.AddSingleton<IDoctorRepository>(new DoctorRepositoryStub(doctorExists));

                services.RemoveAll<IAvailabilityReader>();
                services.AddSingleton<IAvailabilityReader>(new AvailabilityReaderStub());

                services.AddLogging(logging => logging.ClearProviders());
            }));

        return testFactory.CreateClient();
    }

    private sealed class DoctorRepositoryStub(bool isActive) : IDoctorRepository
    {
        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(isActive);

        public void Add(Doctor doctor)
        {
        }

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Doctor?>(null);

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
        {
        }
    }

    private sealed class AvailabilityReaderStub : IAvailabilityReader
    {
        public Task<IReadOnlySet<DateTimeOffset>> GetOccupiedSlotsAsync(
            Guid doctorId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<DateTimeOffset>>(new HashSet<DateTimeOffset>());
    }

    private sealed class TestAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
        public string Role { get; set; } = "PATIENT";
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<TestAuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims =
            [
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("user_role", Options.Role),
            ];
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme, ClaimTypes.Name, "user_role"));
            var ticket = new AuthenticationTicket(principal, Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
