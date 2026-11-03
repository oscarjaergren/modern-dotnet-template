using ErrorOr;

namespace Api.Features.Bookings;

/// <summary>
/// The slice's logic, free of HTTP so it can be unit tested directly.
/// </summary>
internal static class BookingDesk
{
    internal static ErrorOr<BookingResponse> Book(BookingRequest request, DateOnly today)
    {
        if (request.Arrival < today)
        {
            return Error.Validation("Bookings.ArrivalInPast", "Arrival must be today or later.");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        return new BookingResponse(
            $"BK-{request.Arrival:yyyyMMdd}-{request.Nights:D2}",
            request.GuestName,
            request.Arrival,
            request.Arrival.AddDays(request.Nights),
            note);
    }
}
