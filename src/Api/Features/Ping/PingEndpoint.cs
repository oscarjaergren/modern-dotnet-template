using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.Ping;

/// <summary>
/// The simplest slice: proves routing, JSON, OpenAPI and the container end to end. Delete it once
/// real slices do that.
/// </summary>
internal static class PingEndpoint
{
    internal static IEndpointRouteBuilder MapPing(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ping", Handle)
            .WithName("Ping")
            .WithSummary("Returns pong.")
            .WithDescription("Exercises routing and JSON serialization end to end. Not a health probe: use /alive and /health.");

        return app;
    }

    private static Ok<PingResponse> Handle(TimeProvider timeProvider) =>
        TypedResults.Ok(new PingResponse("pong", timeProvider.GetUtcNow()));
}
