using MedicalAppointments.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedicalAppointments.IntegrationTests;

// Direct unit tests against ApiExceptionHandler.TryHandleAsync - no WebApplicationFactory
// needed, since a 413/415 from ASP.NET Core's own request-body limits is impractical to trigger
// reliably through TestServer.
public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WithBadHttpRequestException_PreservesItsOwnStatusCode()
    {
        var handler = new ApiExceptionHandler(new RecordingProblemDetailsService(), NullLogger<ApiExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        var exception = new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);

        bool handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_WithBadHttpRequestExceptionDefaultStatusCode_Returns400()
    {
        var handler = new ApiExceptionHandler(new RecordingProblemDetailsService(), NullLogger<ApiExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        var exception = new BadHttpRequestException("Required parameter \"from\" was not provided.");

        bool handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) => ValueTask.FromResult(true);

        public ValueTask WriteAsync(ProblemDetailsContext context) => ValueTask.CompletedTask;
    }
}
