using Xunit;

namespace MCP.Tests;

public sealed class GreetingToolsTests
{
    [Theory]
    [InlineData("Ada", "Hello, Ada! Nice to meet you.")]
    [InlineData("  Grace Hopper  ", "Hello, Grace Hopper! Nice to meet you.")]
    public void Greet_ReturnsTrimmedPersonalizedGreeting(string name, string expected)
    {
        Assert.Equal(expected, GreetingTools.Greet(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Greet_RejectsBlankName(string name)
    {
        Assert.Throws<ArgumentException>(() => GreetingTools.Greet(name));
    }
}
