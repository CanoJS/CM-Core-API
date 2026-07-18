using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public async Task OpenApiDocument_InProductionByDefault_IsNotExposed()
    {
        // WebApplicationFactory<Program> defaults to the "Development" environment unless told
        // otherwise, which is the opposite of what an App Runner deploy actually runs - so this
        // must explicitly set "Production" to reproduce that default instead of relying on the
        // test host's own default.
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient productionClient = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureLogging(logging => logging.ClearProviders());
            })
            .CreateClient();

        HttpResponseMessage response = await productionClient.GetAsync("/openapi/v1.json", CancellationToken.None);

        // Not mapped, so the fallback "authenticated user required" policy applies to the
        // otherwise-unmatched route before routing would even get to a 404 - either way, the
        // document itself is never served.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_InProductionWithOpenApiEnabledFlag_IsExposedWithoutSwitchingEnvironment()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient enabledClient = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["OpenApi:Enabled"] = "true",
                    }));
            })
            .CreateClient();

        HttpResponseMessage response = await enabledClient.GetAsync("/openapi/v1.json", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
