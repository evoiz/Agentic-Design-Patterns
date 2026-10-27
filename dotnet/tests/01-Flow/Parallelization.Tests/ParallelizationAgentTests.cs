using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.AI.AgentServer.Core;
using Chapter03.Parallelization;
using Chapter03.Parallelization.Hosted;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using Xunit;

namespace AgenticDesignPatterns.Parallelization.Tests;

public sealed class ParallelizationAgentTests
{
    [Theory]
    [InlineData(ParallelizationScenario.SustainableTechnology, "renewable", "electric vehicle", "carbon capture")]
    [InlineData(ParallelizationScenario.TopicAnalysis, "summarize", "questions", "key terms")]
    public async Task Parallel_workers_receive_the_caller_input_and_synthesis_is_returned(
        ParallelizationScenario scenario,
        string first,
        string second,
        string third)
    {
        using var client = new ScriptedChatClient();
        var output = new List<string>();
        const string request = "Caller-supplied topic.";

        var response = await ParallelizationWorkflow.RunAsync(
            client,
            scenario,
            request,
            output.Add);

        Assert.Equal("Synthesized response.", response);
        Assert.Contains(
            output,
            item => item.Contains("Final synthesized response", StringComparison.Ordinal));
        Assert.Contains(client.Requests, item => item.Input == request && item.Instructions.Contains(first, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(client.Requests, item => item.Input == request && item.Instructions.Contains(second, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(client.Requests, item => item.Input == request && item.Instructions.Contains(third, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(client.Requests, item => item.Input.Contains("Renewable result.", StringComparison.Ordinal) ||
            item.Input.Contains("Summary result.", StringComparison.Ordinal));
        Assert.Contains(client.Requests, item => item.Input.Contains("Electric vehicle result.", StringComparison.Ordinal) ||
            item.Input.Contains("Questions result.", StringComparison.Ordinal));
        Assert.Contains(client.Requests, item => item.Input.Contains("Carbon capture result.", StringComparison.Ordinal) ||
            item.Input.Contains("Key terms result.", StringComparison.Ordinal));
        var expectedResults = scenario == ParallelizationScenario.SustainableTechnology
            ? new[]
            {
                "[Renewable energy findings]\nRenewable result.",
                "[Electric vehicle findings]\nElectric vehicle result.",
                "[Carbon capture findings]\nCarbon capture result."
            }
            : new[]
            {
                "[Topic summary]\nSummary result.",
                "[Related questions]\nQuestions result.",
                "[Key terms]\nKey terms result."
            };
        foreach (var expectedResult in expectedResults)
        {
            Assert.Contains(output, item => item.Contains(expectedResult, StringComparison.Ordinal));
        }

        Assert.Equal(4, client.Requests.Count);
        Assert.Equal(0, client.StreamingCalls);
    }

    [Fact]
    public async Task Buffered_workers_still_run_concurrently()
    {
        var started = 0;
        var allWorkersStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new ScriptedChatClient
        {
            BeforeResponse = async cancellationToken =>
            {
                if (Interlocked.Increment(ref started) == 3)
                {
                    allWorkersStarted.SetResult();
                }

                await allWorkersStarted.Task.WaitAsync(cancellationToken);
            }
        };

        var result = await ParallelizationWorkflow.RunAsync(
            client, ParallelizationScenario.TopicAnalysis, "City planning.",
            cancellationToken: timeout.Token);

        Assert.Equal("Synthesized response.", result);
        Assert.Equal(4, started);
        Assert.Equal(0, client.StreamingCalls);
    }

    [Fact]
    public async Task Streaming_host_returns_synthesis_without_opening_model_streams()
    {
        using var client = new ScriptedChatClient();
        var agent = new ParallelizationAgent(client);
        var updates = new List<string>();

        await foreach (var update in agent.RunStreamingAsync("topic: City planning."))
        {
            updates.Add(update.Text);
        }

        Assert.Equal("Synthesized response.", string.Concat(updates));
        Assert.Equal(4, client.Requests.Count);
        Assert.Equal(0, client.StreamingCalls);
    }

    [Fact]
    public async Task Model_failure_is_reported_without_partial_synthesis()
    {
        using var client = new ScriptedChatClient
        {
            CompletionFailure = new HttpRequestException("Connection reset by peer.")
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ParallelizationWorkflow.RunAsync(client, ParallelizationScenario.TopicAnalysis, "City planning."));

        Assert.Contains("Connection reset by peer.", exception.ToString());
        Assert.DoesNotContain(client.Requests, request =>
            request.Instructions.Contains("Synthesize", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, client.StreamingCalls);
    }

    [Fact]
    public async Task Cancelled_request_does_not_call_the_model()
    {
        using var client = new ScriptedChatClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ParallelizationWorkflow.RunAsync(
                client, ParallelizationScenario.TopicAnalysis, "City planning.",
                cancellationToken: cancellation.Token));

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task Responses_protocol_returns_synthesis_for_the_callers_topic()
    {
        using var model = new ScriptedChatClient();
        using var chatClient = model.AsBuilder()
            .UseOpenTelemetry(
                sourceName: ParallelizationWorkflow.TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.ConfigureTracing(tracing => tracing.AddSource(ParallelizationWorkflow.TelemetrySourceName));
        builder.Services.AddFoundryResponses(new ParallelizationAgent(chatClient));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();

        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter03-parallelization",
                input = "topic: Caller-supplied city planning request.",
                stream = false,
                store = false
            });
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
            var responseText = string.Concat(
                body.RootElement.GetProperty("output").EnumerateArray()
                    .SelectMany(item => item.GetProperty("content").EnumerateArray())
                    .Where(item => item.GetProperty("type").GetString() == "output_text")
                    .Select(item => item.GetProperty("text").GetString()));
            Assert.Contains("Synthesized response.", responseText);
            Assert.Contains(model.Requests, item => item.Input == "Caller-supplied city planning request.");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Blank_request_is_rejected_without_a_model_call()
    {
        using var client = new ScriptedChatClient();
        var agent = new ParallelizationAgent(client);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            agent.RunAsync([new ChatMessage(ChatRole.Assistant, "No user request.")]));

        Assert.Empty(client.Requests);
    }

    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly ConcurrentQueue<(string Instructions, string Input)> _requests = new();
        private int _streamingCalls;
        public IReadOnlyList<(string Instructions, string Input)> Requests => _requests.ToArray();
        public int StreamingCalls => _streamingCalls;
        public Exception? CompletionFailure { get; init; }
        public Func<CancellationToken, Task>? BeforeResponse { get; init; }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageList = messages.ToList();
            var input = messageList.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            var instructions = options?.Instructions
                ?? string.Join('\n', messageList.Where(message => message.Role == ChatRole.System).Select(message => message.Text));
            _requests.Enqueue((instructions, input));
            if (CompletionFailure is not null)
            {
                throw CompletionFailure;
            }

            if (BeforeResponse is not null)
            {
                await BeforeResponse(cancellationToken);
            }

            var response = instructions.Contains("synthes", StringComparison.OrdinalIgnoreCase) ||
                instructions.Contains("Overall Conclusion", StringComparison.OrdinalIgnoreCase)
                ? "Synthesized response."
                : instructions.Contains("renewable energy", StringComparison.OrdinalIgnoreCase)
                    ? "Renewable result."
                    : instructions.Contains("electric vehicle", StringComparison.OrdinalIgnoreCase)
                        ? "Electric vehicle result."
                        : instructions.Contains("carbon capture", StringComparison.OrdinalIgnoreCase)
                            ? "Carbon capture result."
                            : instructions.Contains("summarize", StringComparison.OrdinalIgnoreCase)
                                ? "Summary result."
                                : instructions.Contains("three interesting questions", StringComparison.OrdinalIgnoreCase)
                                    ? "Questions result."
                                    : instructions.Contains("5-10 key terms", StringComparison.OrdinalIgnoreCase)
                                        ? "Key terms result."
                                        : throw new InvalidOperationException("No scripted response matches the agent instructions.");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _streamingCalls);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Incomplete streamed response.");
            await Task.Yield();
            throw new IOException("Unable to read data from the transport connection: Connection reset by peer.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
