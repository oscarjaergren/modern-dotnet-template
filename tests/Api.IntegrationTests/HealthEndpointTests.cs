using System.Net;

namespace Api.IntegrationTests;

/// <summary>
/// The probes a deployment calls. The exact-match body assertion is the point: a detailed writer
/// would name every check, and this fails the moment the body says more than the status.
/// </summary>
[Collection(nameof(ApiCollection))]
public class HealthEndpointTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Probe_body_is_the_status_and_nothing_else(string path)
    {
        var response = await fixture.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
