using Api.Features.Bookings;
using ErrorOr;

namespace Api.UnitTests.Features.Bookings;

public class BookingDeskTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);

    private static BookingRequest Request(DateOnly arrival, int nights = 1, string? note = null) =>
        new() { GuestName = "Ada", Arrival = arrival, Nights = nights, Note = note };

    [Fact]
    public void Departure_is_arrival_plus_the_nights()
    {
        var booking = BookingDesk.Book(Request(Today, nights: 3), Today).Value;

        Assert.Equal(new DateOnly(2026, 11, 4), booking.Departure);
    }

    [Fact]
    public void The_reference_encodes_arrival_and_nights()
    {
        var booking = BookingDesk.Book(Request(Today, nights: 3), Today).Value;

        Assert.Equal("BK-20261101-03", booking.Reference);
    }

    [Fact]
    public void An_arrival_in_the_past_is_a_validation_error()
    {
        var error = Assert.Single(BookingDesk.Book(Request(Today.AddDays(-1)), Today).Errors);

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Bookings.ArrivalInPast", error.Code);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  late check-in ", "late check-in")]
    public void The_note_is_trimmed_and_blank_becomes_null(string? note, string? expected) =>
        Assert.Equal(expected, BookingDesk.Book(Request(Today, note: note), Today).Value.Note);
}
