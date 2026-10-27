using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.AI.AgentServer.Core;
using Chapter02.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using Xunit;

namespace AgenticDesignPatterns.Routing.Tests;

public sealed class RoutingAgentTests
{
    [Theory]
    [InlineData("BOOKER", "Booker")]
    [InlineData("INFO", "Info")]
    [InlineData("UNCLEAR", null)]
    public async Task Telemetry_captures_each_executed_agent_and_sensitive_content(
        string decision, string? specialist)
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RoutingWorkflow.TelemetrySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        using var parent = new Activity("routing-telemetry-test").Start();
        using var model = new ScriptedChatClient(decision, "Test specialist answer.");
        using var client = model.AsBuilder()
            .UseOpenTelemetry(sourceName: RoutingWorkflow.TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();
        const string request = "Telemetry test request.";

        await new RoutingAgent(client).RunAsync(request);

        var spans = activities.Where(activity => activity.TraceId == parent.TraceId).ToList();
        var agentSpans = spans.Where(activity => Equals(activity.GetTagItem("gen_ai.operation.name"), "invoke_agent")).ToList();
        Assert.Equal(specialist is null ? 1 : 2, agentSpans.Count);
        Assert.Contains(agentSpans, activity => Equals(activity.GetTagItem("gen_ai.agent.name"), "CoordinatorRouter"));
        if (specialist is not null)
        {
            Assert.Contains(agentSpans, activity => Equals(activity.GetTagItem("gen_ai.agent.name"), specialist));
        }

        var chatSpans = spans.Where(activity => Equals(activity.GetTagItem("gen_ai.operation.name"), "chat")).ToList();
        Assert.Equal(model.Requests.Count, chatSpans.Count);
        Assert.All(chatSpans, activity =>
        {
            Assert.Contains(agentSpans, agent => agent.SpanId == activity.ParentSpanId);
            var payload = JsonSerializer.Serialize(new
            {
                Tags = activity.TagObjects.ToArray(),
                Events = activity.Events.Select(item => new { item.Name, Tags = item.Tags.ToArray() })
            });
            Assert.Contains(request, payload);
        });
        var capturedContent = JsonSerializer.Serialize(spans.Select(activity => new
        {
            Tags = activity.TagObjects.ToArray(),
            Events = activity.Events.Select(item => new { item.Name, Tags = item.Tags.ToArray() })
        }));
        Assert.Contains(decision, capturedContent);
        if (specialist is not null)
        {
            Assert.Contains("Test specialist answer.", capturedContent);
        }
    }

    [Theory]
    [InlineData("BOOKER", "Book me a hotel in Paris.", "Booking simulated.")]
    [InlineData("INFO", "What is the highest mountain?", "Mount Everest.")]
    public async Task Specialist_receives_original_request_and_returns_only_final_output(
        string decision, string request, string answer)
    {
        using var client = new ScriptedChatClient($"{decision}\nREASON: Test decision.", answer);
        var agent = new RoutingAgent(client);

        var response = await agent.RunAsync(request);

        Assert.Equal(answer, response.Text);
        Assert.Equal(2, client.Requests.Count);
        Assert.All(client.Requests, messages =>
            Assert.Equal(request, messages.Last(message => message.Role == ChatRole.User).Text));
    }

    [Theory]
    [InlineData("UNCLEAR")]
    [InlineData("unexpected token")]
    public async Task Unclear_or_unknown_route_does_not_call_specialist(string decision)
    {
        using var client = new ScriptedChatClient(decision);
        var response = await new RoutingAgent(client).RunAsync("Can you help me with this?");

        Assert.Contains("Please clarify", response.Text);
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task Latest_user_message_is_routed_without_previous_request_state()
    {
        using var client = new ScriptedChatClient("INFO", "First answer.", "BOOKER", "Booking simulated.");
        var agent = new RoutingAgent(client);
        var session = await agent.CreateSessionAsync();
        await agent.RunAsync("A general question", session);

        var response = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, "Old question"),
             new ChatMessage(ChatRole.Assistant, "Old answer"),
             new ChatMessage(ChatRole.User, "Book a flight to Tokyo")],
            session);

        Assert.Equal("Booking simulated.", response.Text);
        Assert.All(client.Requests.Skip(2), messages =>
            Assert.Equal("Book a flight to Tokyo", messages.Last(message => message.Role == ChatRole.User).Text));
    }

    [Fact]
    public async Task Streaming_returns_completed_specialist_output()
    {
        using var client = new ScriptedChatClient("INFO", "Mount Everest.");
        var updates = new List<string>();
        await foreach (var update in new RoutingAgent(client).RunStreamingAsync("Highest mountain?"))
        {
            updates.Add(update.Text);
        }

        Assert.Equal(["Mount Everest."], updates);
    }

    [Fact]
    public async Task Missing_user_input_is_rejected_without_model_call()
    {
        using var client = new ScriptedChatClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new RoutingAgent(client).RunAsync([new ChatMessage(ChatRole.Assistant, "Not a request")]));
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task Cancelled_request_does_not_call_model()
    {
        using var client = new ScriptedChatClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RoutingAgent(client).RunAsync("A question", cancellationToken: cancellation.Token));
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task Model_failure_is_not_returned_as_success()
    {
        using var client = new ScriptedChatClient();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RoutingAgent(client).RunAsync("A question"));
        Assert.Contains("failed", error.Message);
    }

    [Theory]
    [InlineData("BOOKER", "Book me a hotel in Paris.", "Booking simulated.")]
    [InlineData("INFO", "Highest mountain?", "Mount Everest.")]
    [InlineData("UNCLEAR", "Can you help?", "Please clarify")]
    public async Task Responses_protocol_returns_completed_workflow_result(
        string decision, string request, string expected)
    {
        using var model = new ScriptedChatClient(decision, expected);
        using var client = model.AsBuilder()
            .UseOpenTelemetry(sourceName: RoutingWorkflow.TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.ConfigureTracing(tracing => tracing.AddSource(RoutingWorkflow.TelemetrySourceName));
        builder.Services.AddFoundryResponses(new RoutingAgent(client));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();
        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter02-routing",
                input = request,
                stream = false,
                store = false
            });
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
            var text = body.RootElement.GetProperty("output").EnumerateArray()
                .SelectMany(item => item.GetProperty("content").EnumerateArray())
                .Where(item => item.GetProperty("type").GetString() == "output_text")
                .Select(item => item.GetProperty("text").GetString());
            Assert.Contains(expected, string.Concat(text));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class ScriptedChatClient(params string[] responses) : IChatClient
    {
        private readonly Queue<string> _responses = new(responses);
        public List<List<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(messages.ToList());
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("Scripted model response is missing.");
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Routing model calls should be non-streaming.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
