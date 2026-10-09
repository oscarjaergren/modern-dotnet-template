using System.Net.Http.Json;

namespace Api.IntegrationTests.Features.Greetings;

[Collection(nameof(ApiCollection))]
public class GreetingsEndpointTests(ApiFixture fixture)
{
    private Task<HttpResponseMessage> Post(object payload) =>
        fixture.Client.PostAsJsonAsync("/greetings", payload, TestContext.Current.CancellationToken);

    // Covers binding as much as greeting: an omitted optional field must arrive as null.
    [Theory]
    [InlineData(36, "Hello, Ada! You are 36.")]
    [InlineData(null, "Hello, Ada!")]
    public async Task Post_greetings_returns_a_greeting(int? age, string expected)
    {
        using var response = await Post(age is null ? new { name = "Ada" } : new { name = "Ada", age });
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, body.RootElement.GetProperty("message").GetString());
    }

    // Returned, not thrown. Also proves ProblemDetails serialises under the real pipeline, which
    // under AOT needs an ApiJsonSerializerContext entry.
    [Fact]
    public async Task Post_greetings_rejects_a_reserved_name_with_problem_details()
    {
        using var response = await Post(new { name = "admin" });
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("'admin' is reserved.", body.RootElement.GetProperty("detail").GetString());
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

    // The framework rejects this before the endpoint runs, so no slice code writes the error body.
    [Fact]
    public async Task Post_greetings_without_a_body_returns_problem_details()
    {
        using var response = await fixture.Client.PostAsync(
            "/greetings", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // Last-wins would greet "admin" while a proxy that reads the first key saw "Ada".
    [Fact]
    public async Task Post_greetings_rejects_a_repeated_property()
    {
        using var content = new StringContent(
            """{"name":"Ada","name":"admin"}""", System.Text.Encoding.UTF8, "application/json");
        using var response = await fixture.Client.PostAsync(
            "/greetings", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Load-bearing: if validation stops being wired up, every payload here returns 200.
    [Theory]
    [InlineData("", null)]
    [InlineData("Ada", -1)]
    [InlineData("Ada", 200)]
    public async Task Post_greetings_rejects_invalid_input(string name, int? age)
    {
        using var response = await Post(new { name, age });
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.RootElement.TryGetProperty("errors", out var errors));
        Assert.NotEqual(0, errors.GetPropertyCount());
    }
}
