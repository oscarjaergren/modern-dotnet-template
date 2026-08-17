namespace Api.Features.Greetings;

/// <param name="Message">The composed greeting.</param>
/// <param name="GreetedAt">Server time when the greeting was composed.</param>
public sealed record GreetingResponse(string Message, DateTimeOffset GreetedAt);
