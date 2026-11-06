using ConferenceDemos;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class CommandLineTests
{
    [Theory]
    [InlineData("plain")]
    [InlineData("check-data")]
    public async Task Removed_commands_fail_before_inference(string command)
        => Assert.Equal(1, await Program.Main([command]));

    [Fact]
    public async Task Removed_graph_option_fails_before_inference()
        => Assert.Equal(1, await Program.Main(["plan", "--graph"]));

    [Fact]
    public async Task Help_remains_available_without_credentials()
        => Assert.Equal(0, await Program.Main(["--help"]));
}
