using System.ComponentModel.DataAnnotations;

namespace Api.Features.Bookings;

/// <summary>
/// Request body for <c>POST /bookings</c>.
/// </summary>
public sealed record BookingRequest
{
    /// <summary>Name the booking is held under.</summary>
    [Required]
    [StringLength(80, MinimumLength = 1)]
    public required string GuestName { get; init; }

    /// <summary>First night of the stay.</summary>
    [Required]
    public required DateOnly Arrival { get; init; }

    /// <summary>Length of the stay, in nights.</summary>
    [Range(1, 30)]
    public required int Nights { get; init; }

    /// <summary>Optional note for the front desk.</summary>
    [StringLength(200)]
    public string? Note { get; init; }
}
