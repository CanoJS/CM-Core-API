using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MedicalAppointments.IntegrationTests;

// Exercises SupabaseAuthAdminService directly against a fake HttpMessageHandler: no real HTTP
// call is made and no email is ever sent. Test keys/passwords below are inert fixtures (they
// authenticate against nothing); none of these tests assert on or print a real secret's value.
public sealed class SupabaseAuthAdminServiceTests
{
    private static readonly Uri BaseAddress = new("https://project.supabase.co/auth/v1/");
    private static readonly Guid CreatedUserId = Guid.NewGuid();

    // Not a real credential: an inert 40-char placeholder shaped like the modern key format.
    private const string ModernSecretKey = "sb_secret_0000000000000000000000000000";

    // Not a real credential: a syntactically valid but unsigned/empty-claim JWT, used only to
    // exercise the "does this look like a JWT" branch.
    private const string LegacyServiceRoleKey =
        "eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoic2VydmljZV9yb2xlIn0.dGVzdC1zaWduYXR1cmU";

    // Not a real credential: an inert fixture password used only to exercise request shaping.
    private const string TemporaryPassword = "Doctor123456!";

    [Fact]
    public async Task CreateDoctorUserAsync_SendsPostToAdminUsersEndpoint()
    {
        var handler = new FakeHttpMessageHandler((_, _) => SuccessResponse(CreatedUserId));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(new Uri(BaseAddress, "admin/users"), handler.LastRequest.RequestUri);
    }

    [Fact]
    public async Task CreateDoctorUserAsync_SendsEmailPasswordConfirmAndMetadataAsJson()
    {
        var handler = new FakeHttpMessageHandler((_, _) => SuccessResponse(CreatedUserId));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("ana@example.com", body.RootElement.GetProperty("email").GetString());
        Assert.Equal(TemporaryPassword, body.RootElement.GetProperty("password").GetString());
        Assert.True(body.RootElement.GetProperty("email_confirm").GetBoolean());
        JsonElement metadata = body.RootElement.GetProperty("user_metadata");
        Assert.Equal("Ana", metadata.GetProperty("first_name").GetString());
        Assert.Equal("López", metadata.GetProperty("last_name").GetString());
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WithModernSecretKey_SendsApikeyOnlyWithoutBearer()
    {
        var handler = new FakeHttpMessageHandler((_, _) => SuccessResponse(CreatedUserId));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None);

        Assert.Equal(ModernSecretKey, Assert.Single(handler.LastRequest!.Headers.GetValues("apikey")));
        Assert.Null(handler.LastRequest.Headers.Authorization);
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WithLegacyServiceRoleJwt_SendsApikeyAndBearer()
    {
        var handler = new FakeHttpMessageHandler((_, _) => SuccessResponse(CreatedUserId));
        SupabaseAuthAdminService service = CreateService(handler, LegacyServiceRoleKey);

        await service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None);

        Assert.Equal(LegacyServiceRoleKey, Assert.Single(handler.LastRequest!.Headers.GetValues("apikey")));
        Assert.NotNull(handler.LastRequest.Headers.Authorization);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal(LegacyServiceRoleKey, handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WithSuccessfulResponse_ReturnsCreatedUserId()
    {
        var handler = new FakeHttpMessageHandler((_, _) => SuccessResponse(CreatedUserId));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        Guid result = await service.CreateDoctorUserAsync(
            "ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None);

        Assert.Equal(CreatedUserId, result);
    }

    [Fact]
    public async Task DeleteUserAsync_SendsDeleteToAdminUsersEndpoint()
    {
        var handler = new FakeHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await service.DeleteUserAsync(CreatedUserId, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal(new Uri(BaseAddress, $"admin/users/{CreatedUserId}"), handler.LastRequest.RequestUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "email_exists")]
    [InlineData(HttpStatusCode.Conflict, "email_exists")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "email_exists")]
    [InlineData(HttpStatusCode.BadRequest, "user_already_exists")]
    [InlineData(HttpStatusCode.Conflict, "user_already_exists")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "user_already_exists")]
    public async Task CreateDoctorUserAsync_WhenUserAlreadyExists_ThrowsConflictRegardlessOfStatusCode(
        HttpStatusCode status,
        string errorCode)
    {
        var handler = new FakeHttpMessageHandler((_, _) => ErrorResponse(status, errorCode));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None));
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WhenRejectedForOtherReason_ThrowsAuthServiceException()
    {
        var handler = new FakeHttpMessageHandler(
            (_, _) => ErrorResponse(HttpStatusCode.UnprocessableEntity, "validation_failed"));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await Assert.ThrowsAsync<AuthServiceException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None));
    }

    [Theory]
    [InlineData("captcha_failed")]
    [InlineData("validation_failed")]
    public async Task CreateDoctorUserAsync_WhenRejectedForOtherReason_LogsSupabaseErrorCode(string errorCode)
    {
        // Supabase's `error_code` is the only field that tells apart, e.g., CAPTCHA protection
        // being enabled on the project (which a server-to-server admin call can never satisfy)
        // from an actual misconfiguration - it must reach the logs, not just the status code.
        var handler = new FakeHttpMessageHandler(
            (_, _) => ErrorResponse(HttpStatusCode.UnprocessableEntity, errorCode));
        var capturingLogger = new CapturingLogger<SupabaseAuthAdminService>();
        var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        IOptions<SupabaseAuthAdminOptions> options = Options.Create(new SupabaseAuthAdminOptions
        {
            SecretKey = ModernSecretKey,
        });
        var service = new SupabaseAuthAdminService(httpClient, options, capturingLogger);

        await Assert.ThrowsAsync<AuthServiceException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None));

        Assert.Contains(capturingLogger.Messages, message => message.Contains(errorCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WhenHttpRequestFails_ThrowsAuthServiceUnavailableException()
    {
        var handler = new FakeHttpMessageHandler(
            (_, _) => throw new HttpRequestException("connection refused"));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await Assert.ThrowsAsync<AuthServiceUnavailableException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None));
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WhenHttpClientTimesOut_ThrowsAuthServiceUnavailableException()
    {
        // Mirrors what HttpClient.Timeout actually throws: a TaskCanceledException wrapping a
        // TimeoutException, raised without the caller's token being cancelled.
        var handler = new FakeHttpMessageHandler(
            (_, _) => throw new TaskCanceledException("timed out", new TimeoutException()));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await Assert.ThrowsAsync<AuthServiceUnavailableException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None));
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WhenCallerCancels_PreservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHttpMessageHandler((_, cancellationToken) =>
        {
            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return SuccessResponse(CreatedUserId);
        });
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, cts.Token));
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WithInvalidSuccessBody_ThrowsAuthServiceException()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { unexpected = "shape" }),
        }));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await Assert.ThrowsAsync<AuthServiceException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None));
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WithNonJsonSuccessBody_ThrowsAuthServiceException()
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", System.Text.Encoding.UTF8, "application/json"),
        }));
        SupabaseAuthAdminService service = CreateService(handler, ModernSecretKey);

        await Assert.ThrowsAsync<AuthServiceException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None));
    }

    [Fact]
    public async Task CreateDoctorUserAsync_WhenSecretKeyIsMissing_ThrowsAuthServiceUnavailableExceptionWithoutCallingHandler()
    {
        var handler = new FakeHttpMessageHandler((_, _) => SuccessResponse(CreatedUserId));
        SupabaseAuthAdminService service = CreateService(handler, secretKey: null);

        await Assert.ThrowsAsync<AuthServiceUnavailableException>(() =>
            service.CreateDoctorUserAsync("ana@example.com", "Ana", "López", TemporaryPassword, CancellationToken.None));

        Assert.Null(handler.LastRequest);
    }

    private static SupabaseAuthAdminService CreateService(HttpMessageHandler handler, string? secretKey)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        IOptions<SupabaseAuthAdminOptions> options = Options.Create(new SupabaseAuthAdminOptions
        {
            SecretKey = secretKey,
        });

        return new SupabaseAuthAdminService(httpClient, options, NullLogger<SupabaseAuthAdminService>.Instance);
    }

    private static Task<HttpResponseMessage> SuccessResponse(Guid userId) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = userId }),
        });

    private static Task<HttpResponseMessage> ErrorResponse(HttpStatusCode status, string errorCode) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(new { error_code = errorCode, msg = "rejected" }),
        });

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return await responder(request, cancellationToken);
        }
    }
}
