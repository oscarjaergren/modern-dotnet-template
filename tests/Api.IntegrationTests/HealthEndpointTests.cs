using System.Net;

namespace Api.IntegrationTests;

/// <summary>
/// The probes a deployment actually calls. These are mapped in every environment, so the thing
/// worth asserting is not that they answer — it is that they answer with nothing useful to an
/// attacker.
/// </summary>
[Collection(nameof(ApiCollection))]
public class HealthEndpointTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Probe_reports_healthy(string path)
    {
        var response = await fixture.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Probe_does_not_name_its_checks(string path)
    {
        // A detailed response writer would list every registered check by name, which tells an
        // unauthenticated caller what the service depends on. The body is the aggregate status
        // and nothing else — see ServiceDefaults.Extensions.WriteStatusOnly.
        var body = await fixture.Client.GetStringAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("self", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{", body, StringComparison.Ordinal);
    }
}
