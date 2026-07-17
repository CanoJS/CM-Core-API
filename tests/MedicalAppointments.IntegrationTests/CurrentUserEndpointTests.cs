using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Users.GetCurrentUser;
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

public sealed class CurrentUserEndpointTests
{
    private static readonly Guid UserId = Guid.Parse("003a9cee-6fc7-4218-b3f0-be99aab3b508");

    [Fact]
    public async Task CurrentUser_WithValidIdentity_ReturnsProfile()
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

                services.RemoveAll<IUserProfileReader>();
                services.AddSingleton<IUserProfileReader>(new UserProfileReaderStub());
                services.AddLogging(logging => logging.ClearProviders());
            }));
        using HttpClient client = testFactory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/users/me",
            CancellationToken.None);
        CurrentUserResponse? profile = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        Assert.Equal(UserId, profile.Id);
        Assert.Equal("Jesus", profile.FirstName);
        Assert.Equal("Cano Mendez", profile.LastName);
        Assert.Equal("PATIENT", profile.Role);
    }

    private sealed class UserProfileReaderStub : IUserProfileReader
    {
        public Task<UserProfileSnapshot?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<UserProfileSnapshot?>(new UserProfileSnapshot(
                userId,
                "Jesus",
                "Cano Mendez",
                "jesus@example.com",
                UserRole.Patient,
                true));
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
                new Claim("sub", UserId.ToString()),
                new Claim("user_role", "PATIENT"),
            ];
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
            var ticket = new AuthenticationTicket(principal, Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
