using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Specialties.ChangeSpecialtyStatus;
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
        using HttpClient client = CreateAuthenticatedClient("PATIENT", new InMemorySpecialtyRepository());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/specialties",
            new { name = "Pediatría" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSpecialty_WithAdminToken_ReturnsCreated()
    {
        using HttpClient client = CreateAuthenticatedClient("ADMIN", new InMemorySpecialtyRepository());

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
            new InMemorySpecialtyRepository(),
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

    [Fact]
    public async Task ChangeStatus_WithoutAccessToken_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/specialties/{Guid.NewGuid()}/status",
            new { active = false, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithPatientToken_ReturnsForbidden()
    {
        var repository = new InMemorySpecialtyRepository();
        Specialty specialty = repository.Seed(new Specialty(Guid.NewGuid(), "Pediatría"), version: 1);
        using HttpClient client = CreateAuthenticatedClient("PATIENT", repository);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/specialties/{specialty.Id}/status",
            new { active = false, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithMalformedVersion_ReturnsBadRequest()
    {
        var repository = new InMemorySpecialtyRepository();
        Specialty specialty = repository.Seed(new Specialty(Guid.NewGuid(), "Pediatría"), version: 1);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", repository);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/specialties/{specialty.Id}/status",
            new { active = false, version = "not-a-number" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithUnknownId_ReturnsNotFound()
    {
        using HttpClient client = CreateAuthenticatedClient("ADMIN", new InMemorySpecialtyRepository());

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/specialties/{Guid.NewGuid()}/status",
            new { active = false, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithAdminTokenAndCurrentVersion_ChangesState()
    {
        var repository = new InMemorySpecialtyRepository();
        Specialty specialty = repository.Seed(new Specialty(Guid.NewGuid(), "Pediatría"), version: 1);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", repository);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/specialties/{specialty.Id}/status",
            new { active = false, version = "1" },
            CancellationToken.None);
        ChangeSpecialtyStatusResponse? body = await response.Content.ReadFromJsonAsync<ChangeSpecialtyStatusResponse>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.Active);
    }

    [Fact]
    public async Task ChangeStatus_WithStaleVersionAndDifferentDesiredState_ReturnsConflict()
    {
        var repository = new InMemorySpecialtyRepository();
        Specialty specialty = repository.Seed(new Specialty(Guid.NewGuid(), "Pediatría"), version: 5);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", repository);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/specialties/{specialty.Id}/status",
            new { active = false, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithStaleVersionAndSameDesiredState_ReturnsConflict()
    {
        var repository = new InMemorySpecialtyRepository();
        Specialty specialty = repository.Seed(new Specialty(Guid.NewGuid(), "Pediatría"), version: 5);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", repository);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/specialties/{specialty.Id}/status",
            new { active = true, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(
        string role,
        InMemorySpecialtyRepository specialtyRepository,
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

                services.RemoveAll<IUnitOfWork>();
                services.AddSingleton<IUnitOfWork>(new InMemoryUnitOfWork(specialtyRepository));

                services.AddLogging(logging => logging.ClearProviders());
            }));

        return testFactory.CreateClient();
    }

    /// <summary>
    /// Stands in for the real EF repository, but mimics its xmin semantics closely enough to
    /// exercise the PATCH endpoint's status-code mapping: a status update only "commits" (and
    /// bumps the version) when the caller's submitted version matches the stored one, regardless
    /// of whether the desired Active value equals the current one.
    /// </summary>
    private sealed class InMemorySpecialtyRepository : ISpecialtyRepository
    {
        private readonly Dictionary<Guid, Specialty> specialties = [];
        private readonly Dictionary<Guid, uint> versions = [];
        private Guid? pendingId;
        private uint? pendingSubmittedVersion;

        public Specialty Seed(Specialty specialty, uint version)
        {
            specialties[specialty.Id] = specialty;
            versions[specialty.Id] = version;
            return specialty;
        }

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Add(Specialty specialty)
        {
            specialties[specialty.Id] = specialty;
            versions[specialty.Id] = 1;
        }

        public Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(specialties.GetValueOrDefault(id));

        public void PrepareStatusUpdate(Specialty specialty, uint version)
        {
            pendingId = specialty.Id;
            pendingSubmittedVersion = version;
        }

        public bool TryCommitPendingUpdate(out uint newVersion)
        {
            newVersion = 0;

            if (pendingId is not { } id || pendingSubmittedVersion is not { } submitted)
            {
                return true;
            }

            if (versions[id] != submitted)
            {
                return false;
            }

            newVersion = submitted + 1;
            versions[id] = newVersion;
            pendingId = null;
            pendingSubmittedVersion = null;
            return true;
        }
    }

    private sealed class InMemoryUnitOfWork(InMemorySpecialtyRepository repository) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (!repository.TryCommitPendingUpdate(out _))
            {
                throw new ConflictException("The resource was changed by another request. Refresh and try again.");
            }

            return Task.FromResult(1);
        }
    }

    private sealed class AdminSpecialtyReaderStub(params AdminSpecialtyItem[] items) : IAdminSpecialtyReader
    {
        public Task<IReadOnlyList<AdminSpecialtyItem>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminSpecialtyItem>>(items);
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
