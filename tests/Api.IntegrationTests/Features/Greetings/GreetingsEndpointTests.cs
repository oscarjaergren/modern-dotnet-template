using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Features.Greetings;

[Collection(nameof(ApiCollection))]
public class GreetingsEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Post_greetings_returns_a_greeting()
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/greetings",
            new { name = "Ada", age = 36 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Hello, Ada! You are 36.", body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Post_greetings_without_age_omits_it()
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/greetings",
            new { name = "Ada" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Hello, Ada!", body.RootElement.GetProperty("message").GetString());
    }

    // An expected failure, returned as a value rather than thrown. It also proves ProblemDetails
    // serializes correctly through the real pipeline — under Native AOT that needs an entry in
    // ApiJsonSerializerContext, and a missing one fails at runtime rather than at build.
    [Fact]
    public async Task Post_greetings_rejects_a_reserved_name_with_problem_details()
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/greetings",
            new { name = "admin" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Name is reserved.", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());

        // Set by CustomizeProblemDetails in Program.cs — this is what ties a failed request back
        // to its OpenTelemetry trace.
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

    // This test is load-bearing, not decorative. .NET 10's AddValidation() only applies to
    // endpoints defined in the same assembly that called it — if that ever stops being true,
    // validation silently does nothing and every one of these payloads would return 200.
    [Theory]
    [InlineData("", null)]           // Name below minimum length
    [InlineData("Ada", -1)]          // Age below range
    [InlineData("Ada", 200)]         // Age above range
    public async Task Post_greetings_rejects_invalid_input(string name, int? age)
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/greetings",
            new { name, age },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.True(body.RootElement.TryGetProperty("errors", out var errors));
        Assert.NotEqual(0, errors.GetPropertyCount());
    }
}
