namespace Api.Features.Ping;

/// <param name="Status">Always <c>pong</c>.</param>
/// <param name="Timestamp">Server time when the request was handled.</param>
public sealed record PingResponse(string Status, DateTimeOffset Timestamp);
