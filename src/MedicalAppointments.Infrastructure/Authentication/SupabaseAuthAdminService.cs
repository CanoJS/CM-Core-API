using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict)
            {
                throw new ConflictException("A user with this email already exists.");
            }

            if (!response.IsSuccessStatusCode)
            {
                LogAuthAdminCallRejected(logger, "invite", (int)response.StatusCode, CorrelationId);
                throw new AuthServiceException("The identity provider rejected the invitation.");
            }

            InviteUserResponse? body = await response.Content.ReadFromJsonAsync<InviteUserResponse>(
                cancellationToken);
            if (body is null || body.Id == Guid.Empty)
            {
                LogAuthAdminCallRejected(logger, "invite", (int)response.StatusCode, CorrelationId);
                throw new AuthServiceException("The identity provider returned an unexpected response.");
            }

            return body.Id;
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

    private static void ApplySecretKey(HttpRequestMessage request, string secretKey)
    {
        request.Headers.Add("apikey", secretKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
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
        Message = "Supabase Auth Admin {Operation} call rejected with status {StatusCode} (trace {TraceId})")]
    private static partial void LogAuthAdminCallRejected(ILogger logger, string operation, int statusCode, string traceId);

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

    private sealed record InviteUserRequest(string Email, [property: JsonPropertyName("data")] InviteUserMetadata Data);

    private sealed record InviteUserMetadata(
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("last_name")] string LastName);

    private sealed record InviteUserResponse(Guid Id);
}
