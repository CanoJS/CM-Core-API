using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Specialties.CreateSpecialty;
using MedicalAppointments.Application.Specialties.GetAdminSpecialties;
using MedicalAppointments.Domain.Specialties;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAppointments.IntegrationTests;

public sealed class AdminSpecialtyEndpointTests
{
    [Fact]
    public async Task CreateSpecialty_WithoutAccessToken_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/specialties",
            new { name = "Pediatría" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateSpecialty_WithPatientToken_ReturnsForbidden()
    {
        using HttpClient client = CreateAuthenticatedClient("PATIENT", new SpecialtyRepositoryStub());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/specialties",
            new { name = "Pediatría" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSpecialty_WithAdminToken_ReturnsCreated()
    {
        using HttpClient client = CreateAuthenticatedClient("ADMIN", new SpecialtyRepositoryStub());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/specialties",
            new { name = "Pediatría" },
            CancellationToken.None);
        CreateSpecialtyResponse? body = await response.Content.ReadFromJsonAsync<CreateSpecialtyResponse>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Pediatría", body.Name);
        Assert.True(body.Active);
    }

    [Fact]
    public async Task GetAdminSpecialties_WithAdminToken_ReturnsActiveAndInactive()
    {
        Guid activeId = Guid.NewGuid();
        Guid inactiveId = Guid.NewGuid();
        var reader = new AdminSpecialtyReaderStub(
            new AdminSpecialtyItem(activeId, "Cardiología", true, 1),
            new AdminSpecialtyItem(inactiveId, "Pediatría", false, 2));
        using HttpClient client = CreateAuthenticatedClient(
            "ADMIN",
            new SpecialtyRepositoryStub(),
            adminSpecialtyReader: reader);

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/admin/specialties",
            CancellationToken.None);
        AdminSpecialtyResponse[]? body = await response.Content.ReadFromJsonAsync<AdminSpecialtyResponse[]>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Length);
        Assert.Contains(body, specialty => specialty.Id == activeId && specialty.Active);
        Assert.Contains(body, specialty => specialty.Id == inactiveId && !specialty.Active);
    }

    private static HttpClient CreateAuthenticatedClient(
        string role,
        SpecialtyRepositoryStub specialtyRepository,
        AdminSpecialtyReaderStub? adminSpecialtyReader = null)
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

                services.RemoveAll<ISpecialtyRepository>();
                services.AddSingleton<ISpecialtyRepository>(specialtyRepository);

                services.RemoveAll<IAdminSpecialtyReader>();
                services.AddSingleton<IAdminSpecialtyReader>(
                    adminSpecialtyReader ?? new AdminSpecialtyReaderStub());

                services.AddSingleton<IUnitOfWork, NoOpUnitOfWork>();
                services.AddLogging(logging => logging.ClearProviders());
            }));

        return testFactory.CreateClient();
    }

    private sealed class SpecialtyRepositoryStub : ISpecialtyRepository
    {
        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Add(Specialty specialty)
        {
        }

        public Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Specialty?>(null);

        public void SetVersion(Specialty specialty, uint version)
        {
        }
    }

    private sealed class AdminSpecialtyReaderStub(params AdminSpecialtyItem[] items) : IAdminSpecialtyReader
    {
        public Task<IReadOnlyList<AdminSpecialtyItem>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminSpecialtyItem>>(items);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
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
