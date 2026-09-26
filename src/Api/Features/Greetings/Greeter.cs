using System.Collections.Frozen;

namespace Api.Features.Greetings;

/// <summary>
/// The slice's logic, free of HTTP so it can be unit tested directly.
/// </summary>
/// <remarks>
/// Static, with no interface: there is nothing to substitute. <see cref="IsReserved"/> returns a
/// value rather than throwing, because a reserved name is an expected outcome; the endpoint turns
/// it into a 409.
/// </remarks>
internal static class Greeter
{
    // A stand-in business rule. The comparer lives in the set, so no call site can forget it.
    private static readonly FrozenSet<string> ReservedNames =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "admin", "root", "system");

    internal static bool IsReserved(string name) => ReservedNames.Contains(name.Trim());

    internal static string Greet(string name, int? age) =>
        age is null
            ? $"Hello, {name}!"
            : $"Hello, {name}! You are {age}.";
}
