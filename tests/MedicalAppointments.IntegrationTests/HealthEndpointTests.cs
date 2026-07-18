using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace MedicalAppointments.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        WebApplicationFactory<Program> testFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.ClearProviders()));

        client = testFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task LiveHealth_ReturnsOk()
    {
        HttpResponseMessage response = await client.GetAsync("/health/live", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_WithoutAccessToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/users/me",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("http://localhost:4200", true)]
    [InlineData("https://not-configured.example", false)]
    public async Task CorsPreflight_UsesConfiguredOrigins(string origin, bool shouldBeAllowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/specialties");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        bool includesOrigin = response.Headers.TryGetValues(
            "Access-Control-Allow-Origin",
            out IEnumerable<string>? allowedOrigins)
            && allowedOrigins.Contains(origin, StringComparer.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(shouldBeAllowed, includesOrigin);
    }
}
