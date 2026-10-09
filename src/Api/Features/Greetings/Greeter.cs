using System.Collections.Frozen;
using ErrorOr;

namespace Api.Features.Greetings;

/// <summary>
/// The slice's logic, free of HTTP so it can be unit tested directly.
/// </summary>
/// <remarks>
/// Static, with no interface: there is nothing to substitute. A reserved name is an expected
/// outcome, so it is returned as an <see cref="Error"/> rather than thrown; the endpoint maps it to
/// a 409.
/// </remarks>
internal static class Greeter
{
    // A stand-in business rule. The comparer lives in the set, so no call site can forget it.
    private static readonly FrozenSet<string> ReservedNames =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "admin", "root", "system");

    internal static ErrorOr<string> Greet(string name, int? age)
    {
        if (ReservedNames.Contains(name.Trim()))
        {
            return Error.Conflict("Greetings.NameReserved", $"'{name}' is reserved.");
        }

        return age is null
            ? $"Hello, {name}!"
            : $"Hello, {name}! You are {age}.";
    }
}
