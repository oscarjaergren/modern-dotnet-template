using Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.Greetings;

/// <summary>
/// The slice to copy: a validated request, a typed result naming every outcome, logic testable
/// without HTTP, and one <c>Map*</c> registration.
/// </summary>
internal static class GreetingsEndpoint
{
    internal static IEndpointRouteBuilder MapGreetings(this IEndpointRouteBuilder app)
    {
        app.MapPost("/greetings", Handle)
            .WithName("CreateGreeting")
            .WithSummary("Composes a greeting.")
            .WithDescription("Demonstrates request validation, a typed response, and slice-local logic.")
            // ProblemHttpResult has no compile-time status, so the 409 must be declared or it is
            // missing from openapi.json.
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    // The return type names every outcome; a thrown exception would appear in neither it nor
    // openapi.json. ValidationProblem is never returned here, since validation runs before the
    // handler, but declaring it documents the 400.
    private static Results<Ok<GreetingResponse>, ValidationProblem, ProblemHttpResult> Handle(
        GreetingRequest request,
        TimeProvider timeProvider)
    {
        var greeting = Greeter.Greet(request.Name, request.Age);
        if (greeting.IsError)
        {
            return greeting.FirstError.ToProblem();
        }

        return TypedResults.Ok(new GreetingResponse(greeting.Value, timeProvider.GetUtcNow()));
    }
}
