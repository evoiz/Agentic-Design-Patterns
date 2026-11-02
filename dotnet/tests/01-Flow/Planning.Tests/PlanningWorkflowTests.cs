using System.Text.Json;
using AgenticDesignPatterns.TestSupport;
using Chapter06.Planning;
using Chapter06.Planning.Hosted;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.Planning.Tests;

public sealed class PlanningWorkflowTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public async Task Plans_at_the_step_boundaries_are_accepted_and_written(int stepCount)
    {
        var steps = Enumerable.Range(1, stepCount).Select(index => $"Step {index}").ToArray();
        var requests = new List<string>();
        using var client = CreateClient(requests, JsonSerializer.Serialize(new { steps }), "A summary following the plan.");
        var (planner, writer) = PlanningAgents.Create(client);

        var result = await PlanningWorkflow.RunAsync(planner, writer, "A test topic.");

        Assert.Equal(steps, result.Steps);
        Assert.Equal("A summary following the plan.", result.Summary);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, input => Assert.Contains("A test topic.", input));
        Assert.Contains("1. Step 1", requests[1]);
        Assert.Contains($"{stepCount}. Step {stepCount}", requests[1]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"steps":null}""")]
    [InlineData("""{"steps":["Step 1","Step 2"]}""")]
    [InlineData("""{"steps":["1","2","3","4","5","6","7"]}""")]
    [InlineData("""{"steps":["1","2"," "]}""")]
    [InlineData("""{"steps":["1","2",null]}""")]
    public async Task Invalid_plans_fail_without_invoking_the_writer(string response)
    {
        var requests = new List<string>();
        using var client = CreateClient(requests, response);
        var (planner, writer) = PlanningAgents.Create(client);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            PlanningWorkflow.RunAsync(planner, writer, "A test topic."));
        Assert.Single(requests);
    }

    [Fact]
    public async Task Hosted_smoke_returns_the_plan_and_summary_for_the_caller()
    {
        var requests = new List<string>();
        using var client = CreateClient(requests, """{"steps":["First","Second","Third"]}""", "Caller summary.");
        await using var host = await HostedResponses.StartAsync(new PlanningAgent(client));
        using var response = await host.InvokeAsync("Caller topic.", "chapter06-planning");
        var text = HostedResponses.OutputText(response.RootElement);
        Assert.Contains("- First", text);
        Assert.Contains("Caller summary.", text);
        Assert.All(requests, input => Assert.Contains("Caller topic.", input));
    }

    private static ScriptedChatClient CreateClient(List<string> requests, params string[] responses)
    {
        var queue = new Queue<string>(responses);
        return new ScriptedChatClient((messages, _) =>
        {
            requests.Add(messages.Last(message => message.Role == ChatRole.User).Text);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, queue.Dequeue()));
        });
    }
}
