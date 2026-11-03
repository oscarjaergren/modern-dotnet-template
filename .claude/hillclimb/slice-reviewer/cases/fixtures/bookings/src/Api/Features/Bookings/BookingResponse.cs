namespace Api.Features.Bookings;

/// <param name="Reference">Quote this when contacting the hotel.</param>
/// <param name="GuestName">Name the booking is held under.</param>
/// <param name="Arrival">First night of the stay.</param>
/// <param name="Departure">The morning the guest leaves.</param>
/// <param name="Note">The front-desk note, trimmed, if one was given.</param>
public sealed record BookingResponse(
    string Reference,
    string GuestName,
    DateOnly Arrival,
    DateOnly Departure,
    string? Note);
