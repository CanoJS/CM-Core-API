using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Doctors.GetActiveDoctors;
using MedicalAppointments.Application.Specialties.GetSpecialties;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAppointments.IntegrationTests;

public sealed class CatalogEndpointTests
{
    private static readonly Guid SpecialtyId = Guid.Parse("4231b0fc-cd6d-4c31-968b-0f4047981510");

    [Fact]
    public async Task CatalogEndpoints_WithValidIdentity_ReturnFrontendContracts()
    {
        await using var factory = new WebApplicationFactory<Program>();
        WebApplicationFactory<Program> testFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.Scheme,
                        _ => { });

                services.RemoveAll<ISpecialtyCatalogReader>();
                services.RemoveAll<IDoctorCatalogReader>();
                services.AddSingleton<ISpecialtyCatalogReader>(new SpecialtyCatalogReaderStub());
                services.AddSingleton<IDoctorCatalogReader>(new DoctorCatalogReaderStub());
                services.AddLogging(logging => logging.ClearProviders());
            }));
        using HttpClient client = testFactory.CreateClient();

        HttpResponseMessage specialtiesResponse = await client.GetAsync(
            "/api/v1/specialties",
            CancellationToken.None);
        HttpResponseMessage doctorsResponse = await client.GetAsync(
            $"/api/v1/doctors?specialtyId={SpecialtyId}",
            CancellationToken.None);
        SpecialtyResponse[]? specialties =
            await specialtiesResponse.Content.ReadFromJsonAsync<SpecialtyResponse[]>(CancellationToken.None);
        DoctorResponse[]? doctors =
            await doctorsResponse.Content.ReadFromJsonAsync<DoctorResponse[]>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, specialtiesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, doctorsResponse.StatusCode);
        Assert.Equal(SpecialtyId, Assert.Single(Assert.IsType<SpecialtyResponse[]>(specialties)).Id);
        DoctorResponse doctor = Assert.Single(Assert.IsType<DoctorResponse[]>(doctors));
        Assert.Equal("Ana López", doctor.FullName);
        Assert.Equal(SpecialtyId, doctor.Specialty.Id);
        Assert.True(doctor.Active);
    }

    private sealed class SpecialtyCatalogReaderStub : ISpecialtyCatalogReader
    {
        public Task<IReadOnlyList<SpecialtyCatalogItem>> GetActiveAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SpecialtyCatalogItem>>(
                [new SpecialtyCatalogItem(SpecialtyId, "Pediatría")]);
    }

    private sealed class DoctorCatalogReaderStub : IDoctorCatalogReader
    {
        public Task<IReadOnlyList<DoctorCatalogItem>> GetActiveAsync(
            Guid? specialtyId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DoctorCatalogItem>>(
                [new DoctorCatalogItem(
                    Guid.Parse("1bc01428-e196-4654-87f2-650816463f30"),
                    "Ana",
                    "López",
                    "ana@example.com",
                    SpecialtyId,
                    "Pediatría",
                    true)]);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims =
            [
                new Claim("sub", "003a9cee-6fc7-4218-b3f0-be99aab3b508"),
                new Claim("user_role", "PATIENT"),
            ];
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
            var ticket = new AuthenticationTicket(principal, Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
