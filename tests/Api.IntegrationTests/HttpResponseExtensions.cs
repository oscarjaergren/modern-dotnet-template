using System.Text.Json;

namespace Api.IntegrationTests;

internal static class HttpResponseExtensions
{
    /// <summary>
    /// The body as JSON. Tests assert the wire format rather than the C# types, so they stay
    /// honest when internals are renamed.
    /// </summary>
    public static async Task<JsonDocument> ReadJsonAsync(this HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
}
