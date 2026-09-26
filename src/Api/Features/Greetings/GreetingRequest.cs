using System.ComponentModel.DataAnnotations;

namespace Api.Features.Greetings;

/// <summary>
/// Request body for <c>POST /greetings</c>.
/// </summary>
/// <remarks>
/// Must be <c>public</c>: the validation source generator skips internal types, and invalid input
/// then returns 200 instead of 400 with no warning anywhere.
/// </remarks>
public sealed record GreetingRequest
{
    /// <summary>Who to greet.</summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>Optional age; included in the greeting when supplied.</summary>
    [Range(0, 130)]
    public int? Age { get; init; }
}
