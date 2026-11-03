using System.Net.Http.Json;

namespace Api.IntegrationTests.Features.Bookings;

[Collection(nameof(ApiCollection))]
public class BookingsEndpointTests(ApiFixture fixture)
{
    private static readonly string FarFuture = "2099-06-01";

    private Task<HttpResponseMessage> Post(object payload) =>
        fixture.Client.PostAsJsonAsync("/bookings", payload, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Post_bookings_returns_the_booking()
    {
        using var response = await Post(new { guestName = "Ada", arrival = FarFuture, nights = 2 });
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BK-20990601-02", body.RootElement.GetProperty("reference").GetString());
        Assert.Equal("2099-06-03", body.RootElement.GetProperty("departure").GetString());
    }

    [Fact]
    public async Task Post_bookings_rejects_an_arrival_in_the_past()
    {
        using var response = await Post(new { guestName = "Ada", arrival = "2000-01-01", nights = 1 });
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Ada", 0)]
    [InlineData("Ada", 31)]
    public async Task Post_bookings_rejects_invalid_input(string guestName, int nights)
    {
        using var response = await Post(new { guestName, arrival = FarFuture, nights });
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Post_bookings_rejects_a_missing_arrival()
    {
        using var response = await Post(new { guestName = "Ada", nights = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_bookings_rejects_an_overlong_note()
    {
        using var response = await Post(new { guestName = "Ada", arrival = FarFuture, nights = 1, note = new string('x', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
