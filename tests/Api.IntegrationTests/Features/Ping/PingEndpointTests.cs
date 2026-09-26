namespace Api.IntegrationTests.Features.Ping;

[Collection(nameof(ApiCollection))]
public class PingEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Get_ping_returns_pong()
    {
        using var response = await fixture.Client.GetAsync("/ping", TestContext.Current.CancellationToken);
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong", body.RootElement.GetProperty("status").GetString());
        Assert.True(body.RootElement.TryGetProperty("timestamp", out _));
    }
}
