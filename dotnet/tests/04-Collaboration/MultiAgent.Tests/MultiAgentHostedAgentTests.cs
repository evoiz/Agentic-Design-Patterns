using Chapter07.MultiAgent;
using Chapter07.MultiAgent.Hosted;
using Xunit;

namespace AgenticDesignPatterns.MultiAgent.Tests;

public sealed class MultiAgentHostedAgentTests
{
    [Theory]
    [InlineData("sequential: Summarize the topic", MultiAgentScenario.Sequential, "Summarize the topic")]
    [InlineData("agent-tool: draw a cat", MultiAgentScenario.AgentTool, "draw a cat")]
    [InlineData("PARALLEL: Review a launch", MultiAgentScenario.Parallel, "Review a launch")]
    public void ParseRequest_extracts_a_scenario_and_task(
        string request,
        MultiAgentScenario expectedScenario,
        string expectedInput)
    {
        var (scenario, input) = MultiAgentHostedAgent.ParseRequest(request);

        Assert.Equal(expectedScenario, scenario);
        Assert.Equal(expectedInput, input);
    }

    [Fact]
    public void ParseRequest_defaults_unprefixed_tasks_to_the_crew_scenario()
    {
        var (scenario, input) = MultiAgentHostedAgent.ParseRequest("  Write about agent collaboration.  ");

        Assert.Equal(MultiAgentScenario.Crew, scenario);
        Assert.Equal("Write about agent collaboration.", input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("parallel:   ")]
    public void ParseRequest_rejects_empty_requests_or_prefixed_tasks(string request)
    {
        Assert.Throws<ArgumentException>(() => MultiAgentHostedAgent.ParseRequest(request));
    }
}
