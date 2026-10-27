using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter06.Planning;
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
        using var client = new ScriptedChatClient(PlanJson(steps), "A summary following the plan.");
        var (planner, writer) = PlanningAgents.Create(client);

        var result = await PlanningWorkflow.RunAsync(planner, writer, "A test topic.");

        Assert.Equal(steps, result.Steps);
        Assert.Equal("A summary following the plan.", result.Summary);
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains("A test topic.", client.Requests[0].Input);
        Assert.Contains("A test topic.", client.Requests[1].Input);
        Assert.Contains("1. Step 1", client.Requests[1].Input);
        Assert.Contains($"{stepCount}. Step {stepCount}", client.Requests[1].Input);
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
        using var client = new ScriptedChatClient(response);
        var (planner, writer) = PlanningAgents.Create(client);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            PlanningWorkflow.RunAsync(planner, writer, "A test topic."));

        Assert.Single(client.Requests);
    }

    private static string PlanJson(IEnumerable<string> steps) =>
        JsonSerializer.Serialize(new { steps });

    private sealed record ModelRequest(string Input);

    private sealed class ScriptedChatClient(params string[] responses) : IChatClient
    {
        private readonly ConcurrentQueue<ModelRequest> _requests = new();
        private readonly ConcurrentQueue<string> _responses = new(responses);

        public IReadOnlyList<ModelRequest> Requests => _requests.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageList = messages.ToList();
            var input = messageList.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            _requests.Enqueue(new(input));
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("No scripted model response remains.");
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var message in response.Messages)
            {
                yield return new ChatResponseUpdate(message.Role, message.Text);
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
