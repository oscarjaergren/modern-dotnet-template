namespace Api.Features.Greetings;

/// <summary>
/// The slice's logic, kept free of HTTP so it can be unit tested directly.
/// </summary>
/// <remarks>
/// Note what this is not: it is not a service registered in DI, and there is no interface for it.
/// A static function with no dependencies needs neither. Reach for DI when you actually have
/// something to substitute.
///
/// Note also what <see cref="IsReserved"/> does not do: it does not throw. A caller asking to greet
/// a reserved name is an expected outcome, not a bug, so it is reported as a value and the endpoint
/// turns it into a 409. See docs/errors-and-failures.md.
/// </remarks>
internal static class Greeter
{
    // A stand-in for a real business rule. The point is the shape, not the rule.
    private static readonly string[] ReservedNames = ["admin", "root", "system"];

    internal static bool IsReserved(string name) =>
        ReservedNames.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);

    internal static string Greet(string name, int? age) =>
        age is null
            ? $"Hello, {name}!"
            : $"Hello, {name}! You are {age}.";
}
