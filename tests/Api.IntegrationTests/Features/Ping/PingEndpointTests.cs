using System.Text.Json;

namespace Api.IntegrationTests.Features.Ping;

[Collection(nameof(ApiCollection))]
public class PingEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Get_ping_returns_pong()
    {
        using var response = await fixture.Client.GetAsync("/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Asserted against the wire format rather than the C# type: this is the contract
        // consumers actually depend on, and it stays honest if the internal types are renamed.
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("pong", body.RootElement.GetProperty("status").GetString());
        Assert.True(body.RootElement.TryGetProperty("timestamp", out _));
    }
}
