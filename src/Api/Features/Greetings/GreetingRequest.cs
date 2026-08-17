using System.ComponentModel.DataAnnotations;

namespace Api.Features.Greetings;

/// <summary>
/// Request body for <c>POST /greetings</c>.
/// </summary>
/// <remarks>
/// Validation is plain DataAnnotations. .NET 10 discovers these through a source generator
/// (wired up by <c>AddValidation()</c> in Program.cs), so a failed request becomes a
/// 400 ValidationProblem without any handler code and without a validation library.
///
/// This type MUST be public. The validation source generator only discovers public types:
/// mark it <c>internal</c> and the generated resolver comes back empty, validation never runs,
/// and invalid payloads quietly return 200 instead of 400. Nothing warns you — no build error,
/// no analyzer, no log line. The convention in this template is therefore that types crossing
/// the HTTP boundary are public, and everything else in a slice stays internal.
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
