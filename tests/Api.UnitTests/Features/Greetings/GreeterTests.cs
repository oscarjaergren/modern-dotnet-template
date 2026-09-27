using Api.Features.Greetings;

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
        var result = Greeter.Greet("Ada", age: null);

        Assert.Equal("Hello, Ada!", result);
    }

    [Fact]
    public void Greet_with_age_includes_it()
    {
        var result = Greeter.Greet("Ada", age: 36);

        Assert.Equal("Hello, Ada! You are 36.", result);
    }

    [Theory]
    [InlineData("Ada")]
    [InlineData("Grace Hopper")]
    [InlineData("Ada-Lovelace")]
    public void Greet_echoes_the_name_verbatim(string name)
    {
        var result = Greeter.Greet(name, age: null);

        Assert.Contains(name, result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("  root  ")]
    [InlineData("system")]
    public void Reserved_names_are_recognised(string name) =>
        Assert.True(Greeter.IsReserved(name));

    [Theory]
    [InlineData("Ada")]
    [InlineData("administrator")]
    public void Ordinary_names_are_not_reserved(string name) =>
        Assert.False(Greeter.IsReserved(name));
}
