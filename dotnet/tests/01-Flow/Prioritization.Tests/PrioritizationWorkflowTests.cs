using AgenticDesignPatterns.TestSupport;
using Chapter20.Prioritization;
using Chapter20.Prioritization.Hosted;
using Chapter20.Prioritization.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace Prioritization.Tests;

public sealed class PrioritizationWorkflowTests
{
    [Theory]
    [InlineData("""{"priority":"P3","assignedTo":"Worker A"}""")]
    [InlineData("""{"priority":"P0","assignedTo":"Unknown worker"}""")]
    [InlineData("""{"assignedTo":"Worker A"}""")]
    [InlineData("""{"priority":0,"assignedTo":"Worker A"}""")]
    [InlineData("not json")]
    public async Task Invalid_proposal_can_be_corrected_once(string invalid)
    {
        var responses = new Queue<string>([invalid, """{"priority":"P0","assignedTo":"worker b"}"""]);
        var requests = new List<string>();
        using var client = new ScriptedChatClient((messages, _) =>
        {
            requests.Add(messages.Last().Text!);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, responses.Dequeue()));
        });
        var schedule = await PrioritizationWorkflow.RunAsync(client, ["Urgent: restore sign-in"]);
        Assert.Equal(TaskPriority.P0, Assert.Single(schedule).Priority);
        Assert.Equal("Worker B", schedule[0].AssignedTo);
        Assert.Equal(2, requests.Count);
        Assert.Contains("previous proposal was invalid", requests[1]);
    }

    [Fact]
    public async Task Invalid_proposals_exhaust_the_two_call_budget()
    {
        var calls = 0;
        using var client = new ScriptedChatClient((_, _) =>
        {
            calls++;
            return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                """{"priority":"P1","assignedTo":"Unknown worker"}"""));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PrioritizationWorkflow.RunAsync(client, ["A task"]));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("First task\nSecond task")]
    [InlineData("""{"tasks":["First task","Second task"]}""")]
    public async Task Hosted_agent_accepts_both_batch_formats(string input)
    {
        var calls = 0;
        using var client = new ScriptedChatClient((_, _) =>
        {
            calls++;
            return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                """{"priority":"P1","assignedTo":null}"""));
        });
        var response = await new PrioritizationAgent(client).RunAsync(input);
        Assert.Contains("TASK-001 | P1 | Worker A | First task", response.Text);
        Assert.Contains("TASK-002 | P1 | Worker A | Second task", response.Text);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Canceled_requests_do_not_call_the_model()
    {
        using var client = new ScriptedChatClient((_, _) =>
            throw new InvalidOperationException("The model must not be called."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PrioritizationWorkflow.RunAsync(client, ["A task"], cancellationToken: cancellation.Token));
    }
}
