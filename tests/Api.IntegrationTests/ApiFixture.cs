using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.IntegrationTests;

/// <summary>
/// Starts the real application through the Aspire AppHost once per test class collection.
/// </summary>
/// <remarks>
/// Starting the distributed application is slow relative to a unit test, so it is shared rather
/// than repeated per test. Everything below the HTTP boundary is real — there are no substitutes.
/// </remarks>
public sealed class ApiFixture : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    private DistributedApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();

        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        _app = await builder.BuildAsync().WaitAsync(Timeout);
        await _app.StartAsync().WaitAsync(Timeout);

        Client = _app.CreateHttpClient("api");
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("api")
            .WaitAsync(Timeout);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
