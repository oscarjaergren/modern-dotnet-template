using Api.Features.Greetings;
using ErrorOr;

namespace Api.UnitTests.Features.Greetings;

/// <summary>
/// Unit tests mirror the source layout: <c>src/Api/Features/Greetings</c> is tested from
/// <c>tests/Api.UnitTests/Features/Greetings</c>. Keeping the shapes identical means an agent
/// asked to "add tests for slice X" knows exactly where the file goes.
/// </summary>
public class GreeterTests
{
    [Fact]
    public void Greet_without_age_omits_it()
    {
        var result = Greeter.Greet("Ada", age: null).Value;

        Assert.Equal("Hello, Ada!", result);
    }

    [Fact]
    public void Greet_with_age_includes_it()
    {
        var result = Greeter.Greet("Ada", age: 36).Value;

        Assert.Equal("Hello, Ada! You are 36.", result);
    }

    [Theory]
    [InlineData("Ada")]
    [InlineData("Grace Hopper")]
    [InlineData("Ada-Lovelace")]
    public void Greet_echoes_the_name_verbatim(string name)
    {
        var result = Greeter.Greet(name, age: null).Value;

        Assert.Contains(name, result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("  root  ")]
    [InlineData("system")]
    public void Greet_returns_a_conflict_for_a_reserved_name(string name)
    {
        var error = Assert.Single(Greeter.Greet(name, age: null).Errors);

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("Greetings.NameReserved", error.Code);
        Assert.Equal($"'{name}' is reserved.", error.Description);
    }

    [Fact]
    public void A_name_containing_a_reserved_one_is_not_reserved() =>
        Assert.False(Greeter.Greet("administrator", age: null).IsError);
}
