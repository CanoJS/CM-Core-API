using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAppointments.Infrastructure.Authentication;

public sealed partial class SupabaseAuthAdminService(
    HttpClient httpClient,
    IOptions<SupabaseAuthAdminOptions> options,
    ILogger<SupabaseAuthAdminService> logger) : IAuthAdminService
{
    public async Task<Guid> InviteDoctorAsync(
        string email,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        string secretKey = RequireSecretKey();

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildInviteUri())
        {
            Content = JsonContent.Create(new InviteUserRequest(
                email,
                new InviteUserMetadata(firstName, lastName))),
        };
        ApplySecretKey(request, secretKey);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            LogAuthAdminCallFailed(logger, exception, "invite");
            throw new AuthServiceUnavailableException("The identity provider is unavailable.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The caller did not cancel: this is HttpClient's own request timeout
            // (TaskCanceledException wrapping a TimeoutException), not a client disconnect.
            LogAuthAdminCallFailed(logger, exception, "invite");
            throw new AuthServiceUnavailableException("The identity provider is unavailable.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Supabase's `error_code` is stable across releases; the HTTP status for the
                // same condition has varied (400/409/422), so branch on the code, not the
                // status. The response body is never logged: it may contain the submitted email.
                // `error_code` itself is a stable enum-like string (e.g. "captcha_failed",
                // "validation_failed"), not PII, and is the one piece of information that lets
                // whoever is on call tell apart "Supabase CAPTCHA protection is on and rejects
                // this server-to-server call" from a genuine misconfiguration - previously it was
                // parsed only to check for the "already exists" case and silently dropped
                // otherwise, so every other rejection reason was indistinguishable in the logs.
                string? errorCode = await TryReadErrorCodeAsync(response, cancellationToken);
                if (errorCode is "email_exists" or "user_already_exists")
                {
                    throw new ConflictException("A user with this email already exists.");
                }

                LogAuthAdminCallRejected(logger, "invite", (int)response.StatusCode, errorCode ?? "unknown", CorrelationId);
                throw new AuthServiceException("The identity provider rejected the invitation.");
            }

            InviteUserResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<InviteUserResponse>(cancellationToken);
            }
            catch (JsonException)
            {
                body = null;
            }

            if (body is null || body.Id == Guid.Empty)
            {
                LogAuthAdminCallRejected(logger, "invite", (int)response.StatusCode, "unexpected_response_shape", CorrelationId);
                throw new AuthServiceException("The identity provider returned an unexpected response.");
            }

            return body.Id;
        }
    }

    private static async Task<string?> TryReadErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            SupabaseErrorBody? body = await response.Content.ReadFromJsonAsync<SupabaseErrorBody>(cancellationToken);
            return body?.ErrorCode;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        string? secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            LogCompensationSkipped(logger, userId, CorrelationId);
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"admin/users/{userId}");
            ApplySecretKey(request, secretKey);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogCompensationRejected(logger, userId, (int)response.StatusCode, CorrelationId);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogCompensationFailed(logger, exception, userId, CorrelationId);
        }
    }

    private static string CorrelationId => Activity.Current?.Id ?? "none";

    private string RequireSecretKey() =>
        string.IsNullOrWhiteSpace(options.Value.SecretKey)
            ? throw new AuthServiceUnavailableException("The identity provider is not configured.")
            : options.Value.SecretKey;

    // Modern secret/publishable keys (`sb_secret_...`, `sb_publishable_...`) are opaque tokens,
    // not JWTs: Supabase rejects a request that also sends one as a bearer token with
    // "Invalid JWT". Only the legacy `service_role` key - a JWT - needs the Authorization
    // header in addition to `apikey`. See Supabase's "Migrating to publishable and secret API
    // keys" guide (supabase.com/docs/guides/getting-started/migrating-to-new-api-keys).
    private static void ApplySecretKey(HttpRequestMessage request, string secretKey)
    {
        request.Headers.Add("apikey", secretKey);

        if (LooksLikeServiceRoleJwt(secretKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        }
    }

    private static bool LooksLikeServiceRoleJwt(string key)
    {
        if (key.StartsWith("sb_secret_", StringComparison.Ordinal) ||
            key.StartsWith("sb_publishable_", StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = key.Split('.');
        return segments.Length == 3 && Array.TrueForAll(segments, IsBase64UrlSegment);
    }

    private static bool IsBase64UrlSegment(string segment)
    {
        if (segment.Length == 0)
        {
            return false;
        }

        foreach (char c in segment)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private Uri BuildInviteUri()
    {
        string? redirectUrl = options.Value.DoctorInviteRedirectUrl;
        return string.IsNullOrWhiteSpace(redirectUrl)
            ? new Uri("invite", UriKind.Relative)
            : new Uri($"invite?redirect_to={Uri.EscapeDataString(redirectUrl)}", UriKind.Relative);
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Error,
        Message = "Supabase Auth Admin {Operation} call could not reach the identity provider")]
    private static partial void LogAuthAdminCallFailed(ILogger logger, Exception exception, string operation);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Supabase Auth Admin {Operation} call rejected with status {StatusCode}, error_code {ErrorCode} (trace {TraceId})")]
    private static partial void LogAuthAdminCallRejected(ILogger logger, string operation, int statusCode, string errorCode, string traceId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "Compensation for user {UserId} failed with status {StatusCode} (trace {TraceId})")]
    private static partial void LogCompensationRejected(ILogger logger, Guid userId, int statusCode, string traceId);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Compensation for user {UserId} could not reach the identity provider (trace {TraceId})")]
    private static partial void LogCompensationFailed(ILogger logger, Exception exception, Guid userId, string traceId);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Error,
        Message = "Compensation for user {UserId} skipped: identity provider not configured (trace {TraceId})")]
    private static partial void LogCompensationSkipped(ILogger logger, Guid userId, string traceId);

    private sealed record InviteUserRequest(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("data")] InviteUserMetadata Data);

    private sealed record InviteUserMetadata(
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("last_name")] string LastName);

    private sealed record InviteUserResponse([property: JsonPropertyName("id")] Guid Id);

    private sealed record SupabaseErrorBody(
        [property: JsonPropertyName("error_code")] string? ErrorCode);
}
