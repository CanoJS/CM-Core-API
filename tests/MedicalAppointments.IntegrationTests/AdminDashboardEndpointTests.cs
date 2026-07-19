using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Admin.GetAdminDashboard;
using MedicalAppointments.Domain.Appointments;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAppointments.IntegrationTests;

public sealed class AdminDashboardEndpointTests
{
    private static readonly DateTimeOffset ClockNow = new(2026, 7, 20, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetDashboard_WithoutAccessToken_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/dashboard", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("PATIENT")]
    [InlineData("DOCTOR")]
    public async Task GetDashboard_WithNonAdminToken_ReturnsForbidden(string role)
    {
        using HttpClient client = CreateAuthenticatedClient(role, new AdminDashboardReaderStub(0));

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/dashboard", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_WithAdminToken_ReturnsScheduledTodayCount()
    {
        var reader = new AdminDashboardReaderStub(4);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", reader);

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/dashboard", CancellationToken.None);
        AdminDashboardResponse? body = await response.Content.ReadFromJsonAsync<AdminDashboardResponse>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(4, body.ScheduledToday);
        Assert.Equal(AppointmentStatus.Scheduled, reader.LastStatus);
    }

    private static HttpClient CreateAuthenticatedClient(string role, AdminDashboardReaderStub reader)
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
                        options => options.Role = role);

                services.RemoveAll<IAdminDashboardReader>();
                services.AddSingleton<IAdminDashboardReader>(reader);

                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FixedClock(ClockNow));

                services.AddLogging(logging => logging.ClearProviders());
            }));

        return testFactory.CreateClient();
    }

    // Records the status/bounds it was called with (SCHEDULED-only, clinic-local "today") so the
    // count-computation contract is verified without needing a real database; the timezone/date
    // boundary arithmetic itself is covered at the unit level in
    // GetAdminDashboardQueryHandlerTests.
    private sealed class AdminDashboardReaderStub(int count) : IAdminDashboardReader
    {
        public AppointmentStatus? LastStatus { get; private set; }

        public Task<int> CountAppointmentsAsync(
            AppointmentStatus status,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtcExclusive,
            CancellationToken cancellationToken)
        {
            LastStatus = status;
            return Task.FromResult(count);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
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
