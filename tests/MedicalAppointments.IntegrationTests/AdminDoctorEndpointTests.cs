using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Doctors.ChangeDoctorSpecialty;
using MedicalAppointments.Application.Doctors.ChangeDoctorStatus;
using MedicalAppointments.Application.Doctors.GetAdminDoctors;
using MedicalAppointments.Application.Doctors.RegisterDoctor;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAppointments.IntegrationTests;

public sealed class AdminDoctorEndpointTests
{
    private static readonly Guid SpecialtyId = Guid.NewGuid();

    [Fact]
    public async Task RegisterDoctor_WithoutAccessToken_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("PATIENT")]
    [InlineData("DOCTOR")]
    public async Task RegisterDoctor_WithNonAdminToken_ReturnsForbidden(string role)
    {
        using HttpClient client = CreateAuthenticatedClient(role, BuildFixture());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDoctor_WithAdminToken_ReturnsCreatedContract()
    {
        using HttpClient client = CreateAuthenticatedClient("ADMIN", BuildFixture());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);
        RegisterDoctorResponse? body = await response.Content.ReadFromJsonAsync<RegisterDoctorResponse>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Ana", body.FirstName);
        Assert.Equal("López", body.LastName);
        Assert.Equal("ana@example.com", body.Email);
        Assert.Equal(SpecialtyId, body.Specialty.Id);
        Assert.True(body.Active);
    }

    [Fact]
    public async Task RegisterDoctor_WithMissingFirstName_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient("ADMIN", BuildFixture());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDoctor_WithoutTemporaryPassword_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient("ADMIN", BuildFixture());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDoctor_WithTooShortTemporaryPassword_ReturnsBadRequest()
    {
        using HttpClient client = CreateAuthenticatedClient("ADMIN", BuildFixture());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new
            {
                firstName = "Ana",
                lastName = "López",
                email = "ana@example.com",
                specialtyId = SpecialtyId,
                temporaryPassword = "short1!",
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDoctor_WhenSpecialtyDoesNotExist_ReturnsNotFound()
    {
        Fixture fixture = BuildFixture();
        fixture.Specialty = null;
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDoctor_WhenSpecialtyIsInactive_ReturnsConflict()
    {
        Fixture fixture = BuildFixture();
        fixture.Specialty!.Deactivate();
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDoctor_WhenEmailAlreadyExists_ReturnsConflict()
    {
        Fixture fixture = BuildFixture();
        fixture.EmailExists = true;
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDoctor_WhenAuthAdminServiceRejects_ReturnsBadGateway()
    {
        Fixture fixture = BuildFixture();
        fixture.AuthAdminService.CreateException = new AuthServiceException("rejected");
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDoctor_WhenAuthAdminServiceIsNotConfigured_ReturnsServiceUnavailable()
    {
        Fixture fixture = BuildFixture();
        fixture.AuthAdminService.CreateException = new AuthServiceUnavailableException("not configured");
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/doctors",
            new { firstName = "Ana", lastName = "López", email = "ana@example.com", specialtyId = SpecialtyId, temporaryPassword = "Doctor123456!" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminDoctors_WithAdminToken_ReturnsActiveAndInactive()
    {
        Guid activeId = Guid.NewGuid();
        Guid inactiveId = Guid.NewGuid();
        Fixture fixture = BuildFixture();
        fixture.AdminDoctorReader = new AdminDoctorReaderStub(
            new AdminDoctorItem(activeId, Guid.NewGuid(), "Ana", "López", "ana@example.com", SpecialtyId, "Pediatría", true, 1),
            new AdminDoctorItem(inactiveId, Guid.NewGuid(), "Luis", "García", "luis@example.com", SpecialtyId, "Pediatría", false, 2));
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/doctors", CancellationToken.None);
        AdminDoctorResponse[]? body = await response.Content.ReadFromJsonAsync<AdminDoctorResponse[]>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Length);
        Assert.Contains(body, doctor => doctor.Id == activeId && doctor.Active);
        Assert.Contains(body, doctor => doctor.Id == inactiveId && !doctor.Active);
    }

    [Theory]
    [InlineData("PATIENT")]
    [InlineData("DOCTOR")]
    public async Task GetAdminDoctors_WithNonAdminToken_ReturnsForbidden(string role)
    {
        using HttpClient client = CreateAuthenticatedClient(role, BuildFixture());

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/doctors", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithoutAccessToken_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{Guid.NewGuid()}/status",
            new { active = false, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("PATIENT")]
    [InlineData("DOCTOR")]
    public async Task ChangeStatus_WithNonAdminToken_ReturnsForbidden(string role)
    {
        Fixture fixture = BuildFixture();
        Doctor doctor = fixture.DoctorRepository.Seed(
            new Doctor(Guid.NewGuid(), Guid.NewGuid(), SpecialtyId),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient(role, fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor.Id}/status",
            new { active = false, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithUnknownId_ReturnsNotFound()
    {
        using HttpClient client = CreateAuthenticatedClient("ADMIN", BuildFixture());

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{Guid.NewGuid()}/status",
            new { active = false, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithMalformedVersion_ReturnsBadRequest()
    {
        Fixture fixture = BuildFixture();
        Doctor doctor = fixture.DoctorRepository.Seed(
            new Doctor(Guid.NewGuid(), Guid.NewGuid(), SpecialtyId),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor.Id}/status",
            new { active = false, version = "not-a-number" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithAdminTokenAndCurrentVersion_ChangesState()
    {
        Fixture fixture = BuildFixture();
        Doctor doctor = fixture.DoctorRepository.Seed(
            new Doctor(Guid.NewGuid(), Guid.NewGuid(), SpecialtyId),
            version: 1);
        fixture.UserProfileRepository = new UserProfileRepositoryStub(
            new UserProfile(doctor.UserId, "Ana", "López", "ana@example.com", UserRole.Doctor));
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor.Id}/status",
            new { active = false, version = "1" },
            CancellationToken.None);
        ChangeDoctorStatusResponse? body = await response.Content.ReadFromJsonAsync<ChangeDoctorStatusResponse>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.Active);
    }

    [Fact]
    public async Task ChangeStatus_WithStaleVersionAndSameDesiredState_ReturnsConflict()
    {
        Fixture fixture = BuildFixture();
        Doctor doctor = fixture.DoctorRepository.Seed(
            new Doctor(Guid.NewGuid(), Guid.NewGuid(), SpecialtyId),
            version: 5);
        fixture.UserProfileRepository = new UserProfileRepositoryStub(
            new UserProfile(doctor.UserId, "Ana", "López", "ana@example.com", UserRole.Doctor));
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor.Id}/status",
            new { active = true, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSpecialty_WithAdminTokenAndCurrentVersion_ChangesSpecialty()
    {
        Fixture fixture = BuildFixture();
        Doctor doctor = fixture.DoctorRepository.Seed(
            new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor.Id}/specialty",
            new { specialtyId = SpecialtyId, version = "1" },
            CancellationToken.None);
        ChangeDoctorSpecialtyResponse? body = await response.Content.ReadFromJsonAsync<ChangeDoctorSpecialtyResponse>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(SpecialtyId, body.Specialty.Id);
    }

    [Fact]
    public async Task ChangeSpecialty_WithStaleVersionAndSameSpecialty_ReturnsConflict()
    {
        Fixture fixture = BuildFixture();
        Doctor doctor = fixture.DoctorRepository.Seed(
            new Doctor(Guid.NewGuid(), Guid.NewGuid(), SpecialtyId),
            version: 5);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor.Id}/specialty",
            new { specialtyId = SpecialtyId, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSpecialty_WhenSpecialtyDoesNotExist_ReturnsNotFound()
    {
        Fixture fixture = BuildFixture();
        Doctor doctor = fixture.DoctorRepository.Seed(
            new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor.Id}/specialty",
            new { specialtyId = Guid.NewGuid(), version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSpecialty_WhenSpecialtyIsInactive_ReturnsConflict()
    {
        Fixture fixture = BuildFixture();
        fixture.Specialty!.Deactivate();
        Doctor doctor = fixture.DoctorRepository.Seed(
            new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/admin/doctors/{doctor.Id}/specialty",
            new { specialtyId = SpecialtyId, version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static Fixture BuildFixture() => new()
    {
        Specialty = new Specialty(SpecialtyId, "Pediatría"),
        DoctorRepository = new InMemoryDoctorRepository(),
        AuthAdminService = new AuthAdminServiceStub(),
    };

    private static HttpClient CreateAuthenticatedClient(string role, Fixture fixture)
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
                services.AddSingleton<ISpecialtyRepository>(new SpecialtyRepositoryStub(fixture.Specialty));

                services.RemoveAll<IUserProfileRepository>();
                services.AddSingleton<IUserProfileRepository>(
                    fixture.UserProfileRepository ?? new UserProfileRepositoryStub(
                        new UserProfile(
                            fixture.AuthAdminService.CreatedUserId,
                            "Ana",
                            "López",
                            "ana@example.com",
                            UserRole.Patient),
                        emailExists: fixture.EmailExists));

                services.RemoveAll<IDoctorRepository>();
                services.AddSingleton<IDoctorRepository>(fixture.DoctorRepository);

                services.RemoveAll<IAdminDoctorReader>();
                services.AddSingleton<IAdminDoctorReader>(fixture.AdminDoctorReader ?? new AdminDoctorReaderStub());

                services.RemoveAll<IAuthAdminService>();
                services.AddSingleton<IAuthAdminService>(fixture.AuthAdminService);

                services.RemoveAll<IUnitOfWork>();
                services.AddSingleton<IUnitOfWork>(new InMemoryUnitOfWork(fixture.DoctorRepository));

                services.AddLogging(logging => logging.ClearProviders());
            }));

        return testFactory.CreateClient();
    }

    private sealed class Fixture
    {
        public Specialty? Specialty { get; set; }

        public bool EmailExists { get; set; }

        public required InMemoryDoctorRepository DoctorRepository { get; set; }

        public UserProfileRepositoryStub? UserProfileRepository { get; set; }

        public IAdminDoctorReader? AdminDoctorReader { get; set; }

        public required AuthAdminServiceStub AuthAdminService { get; set; }
    }

    private sealed class SpecialtyRepositoryStub(Specialty? specialty) : ISpecialtyRepository
    {
        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Add(Specialty specialty)
        {
        }

        public Task<Specialty?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(specialty?.Id == id ? specialty : null);

        public void PrepareStatusUpdate(Specialty specialty, uint version)
        {
        }
    }

    private sealed class UserProfileRepositoryStub(UserProfile? profile, bool emailExists = false)
        : IUserProfileRepository
    {
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(emailExists);

        public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(profile);
    }

    /// <summary>
    /// Mimics the real repository's xmin semantics for HTTP-level status-code assertions: a
    /// pending status/specialty change only "commits" (and bumps the version) when the
    /// submitted version matches the stored one, regardless of whether the desired value equals
    /// the current one. The genuine EF/Postgres behavior is covered separately in
    /// DoctorRepositoryConcurrencyTests.
    /// </summary>
    private sealed class InMemoryDoctorRepository : IDoctorRepository
    {
        private readonly Dictionary<Guid, Doctor> doctors = [];
        private readonly Dictionary<Guid, uint> versions = [];
        private Doctor? pendingAdd;
        private Guid? pendingId;
        private uint? pendingSubmittedVersion;

        public Doctor Seed(Doctor doctor, uint version)
        {
            doctors[doctor.Id] = doctor;
            versions[doctor.Id] = version;
            return doctor;
        }

        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(doctors.TryGetValue(doctorId, out Doctor? doctor) && doctor.Active);

        public void Add(Doctor doctor) => pendingAdd = doctor;

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(doctors.GetValueOrDefault(id));

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
            pendingId = doctor.Id;
            pendingSubmittedVersion = version;
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
        {
            pendingId = doctor.Id;
            pendingSubmittedVersion = version;
        }

        public bool TryCommitPendingChanges()
        {
            if (pendingAdd is { } added)
            {
                doctors[added.Id] = added;
                versions[added.Id] = 1;
                pendingAdd = null;
                return true;
            }

            if (pendingId is not { } id || pendingSubmittedVersion is not { } submitted)
            {
                return true;
            }

            if (versions[id] != submitted)
            {
                return false;
            }

            versions[id] = submitted + 1;
            pendingId = null;
            pendingSubmittedVersion = null;
            return true;
        }
    }

    private sealed class InMemoryUnitOfWork(InMemoryDoctorRepository repository) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (!repository.TryCommitPendingChanges())
            {
                throw new ConflictException("The resource was changed by another request. Refresh and try again.");
            }

            return Task.FromResult(1);
        }
    }

    private sealed class AdminDoctorReaderStub(params AdminDoctorItem[] items) : IAdminDoctorReader
    {
        public Task<IReadOnlyList<AdminDoctorItem>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminDoctorItem>>(items);
    }

    private sealed class AuthAdminServiceStub : IAuthAdminService
    {
        public Exception? CreateException { get; set; }

        public Guid CreatedUserId { get; set; } = Guid.NewGuid();

        public Task<Guid> CreateDoctorUserAsync(
            string email,
            string firstName,
            string lastName,
            string password,
            CancellationToken cancellationToken) =>
            CreateException is not null
                ? Task.FromException<Guid>(CreateException)
                : Task.FromResult(CreatedUserId);

        public Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
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
