using ResourceOptimization;
using Xunit;

namespace AgenticDesignPatterns.ResourceOptimization.Tests;

public sealed class ResourceOptimizationCommandLineOptionsTests
{
    [Fact]
    public void No_arguments_selects_only_complexity()
    {
        var options = ResourceOptimizationCommandLineOptions.Parse([]);
        Assert.Equal("complexity", options.Mode);
        Assert.Null(options.Deployment);
    }

    [Fact]
    public void Deployment_only_keeps_complexity_default()
    {
        var options = ResourceOptimizationCommandLineOptions.Parse(["--deployment", "fast"]);
        Assert.Equal("complexity", options.Mode);
        Assert.Equal("fast", options.Deployment);
    }

    [Theory]
    [InlineData("complexity")]
    [InlineData("research")]
    [InlineData("all")]
    [InlineData("ALL")]
    public void Explicit_mode_is_preserved_in_either_argument_order(string mode)
    {
        Assert.Equal(mode, ResourceOptimizationCommandLineOptions.Parse([mode, "--deployment", "fast"]).Mode);
        Assert.Equal(mode, ResourceOptimizationCommandLineOptions.Parse(["--deployment", "fast", mode]).Mode);
    }

    [Fact]
    public void Invalid_arguments_are_rejected()
    {
        string[][] invalidArguments =
        [
            ["unknown"],
            ["all", "research"],
            ["--deployment"],
            ["--deployment", ""],
            ["--deployment", "fast", "--deployment", "other"]
        ];
        foreach (var args in invalidArguments)
        {
            Assert.Throws<ArgumentException>(() => ResourceOptimizationCommandLineOptions.Parse(args));
        }
    }
}
