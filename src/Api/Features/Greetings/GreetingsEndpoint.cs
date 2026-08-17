using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.Greetings;

/// <summary>
/// The slice worth copying. It shows the conventions this template cares about: a validated
/// request type, a typed response union that names every outcome, logic that is testable without
/// HTTP, and registration through a single <c>Map*</c> extension.
/// </summary>
internal static class GreetingsEndpoint
{
    internal static IEndpointRouteBuilder MapGreetings(this IEndpointRouteBuilder app)
    {
        app.MapPost("/greetings", Handle)
            .WithName("CreateGreeting")
            .WithSummary("Composes a greeting.")
            .WithDescription("Demonstrates request validation, a typed response, and slice-local logic.")
            // ProblemHttpResult carries no compile-time status code, so nothing can infer the 409
            // and it would be missing from openapi.json. Declare it explicitly. Typed arms like
            // Ok<T> and ValidationProblem are inferred and need no equivalent.
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    // The return type names every way the request can end, and the compiler stops anything else
    // escaping. That is the reason expected failures are returned rather than thrown — a thrown
    // exception appears in neither this signature nor openapi.json, so it is invisible to callers
    // and to reviewers. See docs/errors-and-failures.md.
    //
    // ValidationProblem is never returned from this method; AddValidation() short-circuits the
    // request before the handler runs. Declaring it is what documents the 400.
    private static Results<Ok<GreetingResponse>, ValidationProblem, ProblemHttpResult> Handle(
        GreetingRequest request,
        TimeProvider timeProvider)
    {
        if (Greeter.IsReserved(request.Name))
        {
            return TypedResults.Problem(
                title: "Name is reserved.",
                detail: $"'{request.Name}' cannot be greeted.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Ok(new GreetingResponse(
            Greeter.Greet(request.Name, request.Age),
            timeProvider.GetUtcNow()));
    }
}
