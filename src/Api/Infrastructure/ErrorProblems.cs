using ErrorOr;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Infrastructure;

/// <summary>
/// Turns an <see cref="Error"/> into a ProblemDetails response, so code below the endpoint never
/// picks an HTTP status. See docs/errors-and-failures.md.
/// </summary>
internal static class ErrorProblems
{
    internal static ProblemHttpResult ToProblem(this Error error) =>
        TypedResults.Problem(
            detail: error.Description,
            statusCode: error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            });
}
