using Microsoft.AspNetCore.Diagnostics;

namespace Api.Infrastructure;

/// <summary>
/// Turns an unhandled exception into a ProblemDetails response instead of an empty 500.
/// </summary>
/// <remarks>
/// This is the safety net, not the error-handling strategy. Expected failures should be returned
/// as typed results from the endpoint — see docs/errors-and-failures.md. Anything reaching here is
/// by definition a bug, so it is logged at Error and the client is told nothing about the cause.
///
/// Without this, ASP.NET Core returns a 500 with an empty body: no problem type, no trace id,
/// nothing the caller can act on or correlate with your logs.
/// </remarks>
internal sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    // Source-generated logging: no boxing, no params array, no reflection, and the message
    // template is validated at compile time. This is what CA1848 asks for, which is why that
    // rule is not suppressed in this repo.
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
        // .NET 10 suppresses the middleware's own diagnostics once this returns true, so if this
        // handler does not log, nothing does. See docs/errors-and-failures.md.
        LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Deliberately generic: exception messages leak implementation detail, and a caller can do
        // nothing with them anyway. The traceId is how a report gets tied back to the logs.
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
