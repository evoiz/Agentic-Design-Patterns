using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Extension01.AgentHarness;
using Extension01.AgentHarness.Hosted;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.AgentHarness.Tests;

public sealed class HarnessTests
{
    [Fact]
    public async Task Real_harness_tracks_todos_and_evidence_and_completes()
    {
        using var client = SuccessfulInvestigation();
        var steps = new List<string>();

        var result = await HarnessRunner.RunAsync(client, "Investigate checkout latency.", steps.Add);

        Assert.Equal("completed", result.Status);
        Assert.Null(result.IncompleteReason);
        Assert.Equal(4, result.ModelCalls);
        Assert.False(client.IsDisposed);
        Assert.Equal(3, result.Todos.Count);
        Assert.All(result.Todos, todo => Assert.True(todo.IsComplete));
        Assert.Equal(2, result.Evidence.Count);
        Assert.All(result.Evidence, evidence => Assert.Equal("checkout", evidence.Service));
        Assert.Contains("CHECKOUT-01", result.Answer);
        Assert.Contains(steps, step => step.StartsWith("--- Todo state ---", StringComparison.Ordinal));
        Assert.Contains(client.Requests[0], text => text.Contains("Investigate checkout latency.", StringComparison.Ordinal));
        Assert.Contains("todos_add", client.ToolNames);
        Assert.DoesNotContain("mode_set", client.ToolNames);
        Assert.DoesNotContain(client.ToolNames, name => name.Contains("file", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(client.ToolNames, name => name.Contains("skill", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Premature_model_success_is_not_reported_as_completed()
    {
        using var client = new ScriptedClient(_ => Text("All done, trust me."));
        var result = await HarnessRunner.RunAsync(client, "Investigate checkout.");

        Assert.Equal("incomplete", result.Status);
        Assert.Equal("No work plan was recorded.", result.IncompleteReason);
        Assert.Empty(result.Evidence);
    }

    [Fact]
    public async Task Unfinished_todos_remain_incomplete_after_bounded_loop()
    {
        using var client = new ScriptedClient(
            _ => AddTodos(),
            _ => Text("Still investigating."),
            _ => Text("Still investigating."));

        var result = await HarnessRunner.RunAsync(client, "Investigate checkout.");

        Assert.Equal("incomplete", result.Status);
        Assert.Contains("unfinished", result.IncompleteReason);
        Assert.Equal(3, result.ModelCalls);
        Assert.All(result.Todos, todo => Assert.False(todo.IsComplete));
    }

    [Fact]
    public async Task Evidence_must_include_health_and_runbook_for_the_same_service()
    {
        using var client = new ScriptedClient(
            _ => AddTodos(),
            _ => Calls(
                Call("status", "GetServiceStatus", new() { ["service"] = "checkout" }),
                Call("runbook", "ReadRunbook", new() { ["service"] = "identity" })),
            _ => CompleteTodos(),
            _ => Text("Recommendation."));

        var result = await HarnessRunner.RunAsync(client, "Investigate checkout.");

        Assert.Equal("incomplete", result.Status);
        Assert.Contains("same service", result.IncompleteReason);
    }

    [Theory]
    [InlineData("Investigate identity.")]
    [InlineData("Investigate payments.")]
    [InlineData("Investigate checkout and identity.")]
    [InlineData("Investigate checkoutservice.")]
    public async Task Unrelated_service_evidence_cannot_complete_request(string request)
    {
        using var client = SuccessfulInvestigation();
        var result = await HarnessRunner.RunAsync(client, request);

        Assert.Equal("incomplete", result.Status);
        Assert.NotNull(result.IncompleteReason);
    }

    [Fact]
    public async Task Target_service_matching_is_case_insensitive()
    {
        using var client = SuccessfulInvestigation();
        var result = await HarnessRunner.RunAsync(client, "Investigate CHECKOUT.");
        Assert.Equal("completed", result.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Final recommendation.")]
    public async Task Earlier_loop_progress_is_not_used_as_final_answer(string finalAnswer)
    {
        using var client = new ScriptedClient(
            _ => AddTodos(),
            _ => Calls(
                Call("status", "GetServiceStatus", new() { ["service"] = "checkout" }),
                Call("runbook", "ReadRunbook", new() { ["service"] = "checkout" })),
            _ => Text("Earlier progress report."),
            _ => CompleteTodos(),
            _ => Text(finalAnswer));

        if (finalAnswer.Length == 0)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                HarnessRunner.RunAsync(client, "Investigate checkout."));
            Assert.Contains("no final answer", exception.Message);
        }
        else
        {
            var result = await HarnessRunner.RunAsync(client, "Investigate checkout.");
            Assert.Equal("completed", result.Status);
            Assert.Equal(finalAnswer, result.Answer);
        }
    }

    [Fact]
    public async Task Endless_tool_use_hits_hard_model_call_budget()
    {
        using var client = new ScriptedClient(call => Calls(
            Call($"status-{call}", "GetServiceStatus", new() { ["service"] = "checkout" })));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => HarnessRunner.RunAsync(client, "Keep investigating checkout forever."));

        Assert.Contains("budget", exception.Message);
        Assert.Equal(HarnessRunner.MaximumModelCalls, client.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Empty_requests_never_reach_model(string request)
    {
        using var client = new ScriptedClient(_ => Text("Should not run."));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => HarnessRunner.RunAsync(client, request));
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Oversized_request_never_reaches_model()
    {
        using var client = new ScriptedClient(_ => Text("Should not run."));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HarnessRunner.RunAsync(client, new string('x', HarnessRunner.MaximumRequestCharacters + 1)));
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Request_at_character_limit_is_accepted()
    {
        using var client = new ScriptedClient(_ => Text("No investigation performed."));
        var result = await HarnessRunner.RunAsync(client, new string('x', HarnessRunner.MaximumRequestCharacters));
        Assert.Equal("incomplete", result.Status);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Empty_model_output_fails_explicitly()
    {
        using var client = new ScriptedClient(_ => Text(""));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HarnessRunner.RunAsync(client, "Investigate checkout."));
        Assert.Contains("no final answer", exception.Message);
    }

    [Fact]
    public async Task Cancellation_propagates_to_model()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new ScriptedClient(_ =>
        {
            cancellation.Cancel();
            cancellation.Token.ThrowIfCancellationRequested();
            return Text("Unreachable.");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HarnessRunner.RunAsync(client, "Investigate checkout.", cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Provider_failures_are_not_returned_as_success()
    {
        using var client = new ScriptedClient(_ => throw new HttpRequestException("Model unavailable."));
        await Assert.ThrowsAsync<HttpRequestException>(() => HarnessRunner.RunAsync(client, "Investigate checkout."));
        Assert.False(client.IsDisposed);
    }

    [Fact]
    public void Unsupported_service_produces_no_evidence()
    {
        var tools = new SupportTools();
        Assert.Throws<ArgumentException>(() => tools.ReadRunbook("payments"));
        Assert.Throws<ArgumentException>(() => tools.GetServiceStatus("payments"));
        Assert.Empty(tools.Evidence);
    }

    [Fact]
    public async Task Responses_endpoint_returns_actual_result_and_isolates_requests()
    {
        using var model = SuccessfulInvestigation();
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.Services.AddFoundryResponses(new HarnessHostedAgent(model));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();
        try
        {
            using var http = app.GetTestClient();
            using var first = await http.PostAsJsonAsync("/responses", new
            {
                model = "extension01-agent-harness", input = "Investigate checkout latency.", stream = false, store = false
            });
            first.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
            Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
            using var result = JsonDocument.Parse(OutputText(body.RootElement));
            Assert.Equal("completed", result.RootElement.GetProperty("status").GetString());
            Assert.Equal(2, result.RootElement.GetProperty("evidence").GetArrayLength());

            model.Reset(_ => Text("identity is healthy; no evidence collected."));
            using var second = await http.PostAsJsonAsync("/responses", new
            {
                model = "extension01-agent-harness", input = "Investigate identity.", stream = false, store = false
            });
            second.EnsureSuccessStatusCode();
            using var secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            using var secondResult = JsonDocument.Parse(OutputText(secondBody.RootElement));
            Assert.Equal("incomplete", secondResult.RootElement.GetProperty("status").GetString());
            Assert.Empty(secondResult.RootElement.GetProperty("todos").EnumerateArray());
            Assert.Empty(secondResult.RootElement.GetProperty("evidence").EnumerateArray());
            Assert.DoesNotContain(model.Requests[0], text => text.Contains("checkout latency", StringComparison.Ordinal));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Streaming_host_buffers_the_same_result()
    {
        using var client = SuccessfulInvestigation();
        var agent = new HarnessHostedAgent(client);
        var text = "";
        await foreach (var update in agent.RunStreamingAsync("Investigate checkout."))
        {
            text += update.Text;
        }

        using var result = JsonDocument.Parse(text);
        Assert.Equal("completed", result.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Hosting_isolation_uses_response_id_without_a_user_header()
    {
        var firstContext = new TestResponseContext("first-response");
        var secondContext = new TestResponseContext("second-response");
        Assert.True(string.IsNullOrWhiteSpace(firstContext.PlatformContext.UserIdKey));
#pragma warning disable MAAI001 // Exercise the hosting SDK's experimental isolation contract.
        var provider = new RequestIsolationKeyProvider();
        var first = await provider.GetKeysAsync(firstContext, new CreateResponse());
        var second = await provider.GetKeysAsync(secondContext, new CreateResponse());
        Assert.Equal("request:first-response", first!.UserId);
        Assert.Equal("request:second-response", second!.UserId);
#pragma warning restore MAAI001
    }

    private static string OutputText(JsonElement body) => string.Concat(
        body.GetProperty("output").EnumerateArray()
            .SelectMany(item => item.GetProperty("content").EnumerateArray())
            .Where(item => item.GetProperty("type").GetString() == "output_text")
            .Select(item => item.GetProperty("text").GetString()));

    private static ScriptedClient SuccessfulInvestigation() => new(
        _ => AddTodos(),
        _ => Calls(
            Call("status", "GetServiceStatus", new() { ["service"] = "checkout" }),
            Call("runbook", "ReadRunbook", new() { ["service"] = "checkout" })),
        _ => CompleteTodos(),
        _ => Text("CHECKOUT-01 recommends database on-call review. No changes executed."));

    private static ChatResponse AddTodos() => Calls(Call("todos", "todos_add", new()
    {
        ["todos"] = new[]
        {
            new { title = "Inspect health", description = "Read the service snapshot." },
            new { title = "Read runbook", description = "Read approved guidance." },
            new { title = "Draft recommendations", description = "Recommend next steps." }
        }
    }));

    private static ChatResponse CompleteTodos() => Calls(Call("complete", "todos_complete", new()
    {
        ["items"] = new[]
        {
            new { id = 1, reason = "Inspected health." },
            new { id = 2, reason = "Read runbook." },
            new { id = 3, reason = "Prepared recommendation." }
        }
    }));

    private static FunctionCallContent Call(string id, string name, Dictionary<string, object?> arguments) =>
        new(id, name, arguments);
    private static ChatResponse Calls(params FunctionCallContent[] calls) =>
        new(new ChatMessage(ChatRole.Assistant, calls.Cast<AIContent>().ToList()));
    private static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    private sealed class ScriptedClient(params Func<int, ChatResponse>[] responses) : IChatClient
    {
        private ConcurrentQueue<Func<int, ChatResponse>> _responses = new(responses);
        private int _calls;
        public int Calls => _calls;
        public bool IsDisposed { get; private set; }
        public List<string[]> Requests { get; } = [];
        public HashSet<string> ToolNames { get; } = [];

        public void Reset(params Func<int, ChatResponse>[] next)
        {
            _responses = new(next);
            _calls = 0;
            Requests.Clear();
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(messages.Select(message => message.Text).ToArray());
            foreach (var tool in options?.Tools?.OfType<AIFunction>() ?? [])
            {
                ToolNames.Add(tool.Name);
            }

            var call = Interlocked.Increment(ref _calls);
            var found = _responses.Count > 1
                ? _responses.TryDequeue(out var next)
                : _responses.TryPeek(out next);
            if (!found || next is null)
            {
                throw new InvalidOperationException("No scripted model response remains.");
            }

            return Task.FromResult(next(call));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var message in response.Messages)
            {
                yield return new ChatResponseUpdate(message.Role, message.Text);
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() => IsDisposed = true;
    }

    private sealed class TestResponseContext(string responseId) : ResponseContext(responseId);
}
