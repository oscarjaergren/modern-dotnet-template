using Microsoft.AspNetCore.Diagnostics;

namespace Api.Infrastructure;

/// <summary>
/// Turns an unhandled exception into a ProblemDetails response instead of an empty 500.
/// </summary>
/// <remarks>
/// A safety net for bugs; expected failures are returned from the endpoint. See
/// docs/errors-and-failures.md.
/// </remarks>
internal sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    // Source-generated logging, as CA1848 asks: AOT-safe and checked at compile time.
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception handling {Method} {Path}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string method,
        string path);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // A malformed request, such as a missing or oversized body, is the client's error, not a
        // bug: it keeps its status. Development throws these where production returns them.
        if (exception is BadHttpRequestException badRequest)
        {
            httpContext.Response.StatusCode = badRequest.StatusCode;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = { Status = badRequest.StatusCode },
            });
        }

        // Since .NET 10 the middleware stops logging once this returns true, so log here.
        LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Generic on purpose: exception messages leak internals. The traceId links to the logs.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
            },
        });
    }
}
