using System.Net;
using System.Text.Json;
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

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("https://anything.example")]
    public async Task CorsPreflight_WithAllowAnyOriginFlag_AllowsAnyOrigin(string origin)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient allowAnyClient = factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Cors:AllowAnyOrigin"] = "true",
                    }));
            })
            .CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/specialties");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await allowAnyClient.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(
            "Access-Control-Allow-Origin",
            out IEnumerable<string>? allowedOrigins));
        // AllowAnyOrigin() emits the literal wildcard "*", not an echo of the request's Origin
        // header - unlike WithOrigins(), which is only safe to echo because it validates against
        // an explicit allowlist first.
        Assert.Contains("*", allowedOrigins);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
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

    [Fact]
    public async Task SwaggerUi_InProductionByDefault_IsNotExposed()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient productionClient = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureLogging(logging => logging.ClearProviders());
            })
            .CreateClient();

        HttpResponseMessage response = await productionClient.GetAsync("/swagger/index.html", CancellationToken.None);

        // Swashbuckle's UseSwaggerUI middleware is only registered when OpenApi:Enabled is on
        // (or in Development), so this route is unmatched here - same fallback-policy caveat as
        // OpenApiDocument_InProductionByDefault_IsNotExposed: the UI is never served either way.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerUi_InProductionWithOpenApiEnabledFlag_IsExposedWithoutAuth()
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

        HttpResponseMessage response = await enabledClient.GetAsync("/swagger/index.html", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_WhenEnabled_DeclaresBearerSecuritySchemeAndSkipsHealthLive()
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

        using JsonDocument document = JsonDocument.Parse(
            await enabledClient.GetStringAsync("/openapi/v1.json", CancellationToken.None));

        JsonElement bearerScheme = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearerScheme.GetProperty("bearerFormat").GetString());

        JsonElement paths = document.RootElement.GetProperty("paths");

        // A protected endpoint must reference the Bearer scheme...
        JsonElement specialtiesGet = paths.GetProperty("/api/v1/specialties").GetProperty("get");
        Assert.True(specialtiesGet.TryGetProperty("security", out JsonElement specialtiesSecurity));
        Assert.Contains(
            specialtiesSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        // ...but /health/live (AllowAnonymous) must not be marked as requiring it.
        JsonElement healthLiveGet = paths.GetProperty("/health/live").GetProperty("get");
        Assert.False(healthLiveGet.TryGetProperty("security", out _));
    }
}
