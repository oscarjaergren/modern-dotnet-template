namespace Api.IntegrationTests.Features.Notes;

[Collection(nameof(ApiCollection))]
public class NotesEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Get_note_returns_the_note()
    {
        using var response = await fixture.Client.GetAsync("/notes/1", TestContext.Current.CancellationToken);
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Buy milk", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public async Task Get_note_returns_problem_details_for_an_unknown_id()
    {
        using var response = await fixture.Client.GetAsync("/notes/99", TestContext.Current.CancellationToken);
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }
}
