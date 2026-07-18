using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MedicalAppointments.Api.ErrorHandling;

public sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int statusCode = exception switch
        {
            // ASP.NET Core itself picks the status for a BadHttpRequestException (400 for a
            // missing/malformed parameter, 413 for a body that's too large, 415 for an
            // unsupported media type, ...); preserve it instead of forcing 400 for all of them.
            BadHttpRequestException badRequest => badRequest.StatusCode,
            ArgumentException or DomainException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            AuthServiceException => StatusCodes.Status502BadGateway,
            AuthServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        if (statusCode >= 500)
        {
            LogUnhandledException(
                logger,
                exception,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = GetTitle(statusCode),
                Detail = statusCode >= 500 ? "An unexpected error occurred." : exception.Message,
                Instance = httpContext.Request.Path,
            },
        });
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Invalid request",
        StatusCodes.Status401Unauthorized => "Authentication required",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status409Conflict => "Concurrency conflict",
        StatusCodes.Status413PayloadTooLarge => "Payload too large",
        StatusCodes.Status502BadGateway => "Upstream service error",
        StatusCodes.Status503ServiceUnavailable => "Service unavailable",
        < 500 => "Request error",
        _ => "Server error",
    };

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Method} {Path}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string method,
        PathString path);
}
