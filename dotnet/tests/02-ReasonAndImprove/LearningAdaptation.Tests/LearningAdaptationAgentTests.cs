using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter09.LearningAdaptation;
using Chapter09.LearningAdaptation.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using Xunit;

namespace AgenticDesignPatterns.LearningAdaptation.Tests;

public sealed class LearningAdaptationAgentTests
{
    private const string ProposedInstructions =
        "Use only supplied policies. State return eligibility and timing, and request an order number and damage photo when needed.";

    [Fact]
    public async Task Passing_baseline_skips_curation_and_reports_no_update()
    {
        using var client = new ScriptedChatClient();

        var result = await LearningAdaptationWorkflow.RunAsync(client, ProposedInstructions);

        Assert.Equal(3, result.Baseline.PassedCases);
        Assert.Empty(result.Proposals);
        Assert.Equal(3, client.Requests.Count);
        Assert.Contains(
            "The starting prompt already passes the benchmark; no update is recommended.",
            result.Format());
    }

    [Fact]
    public async Task Adaptation_uses_deterministic_benchmark_and_keeps_proposal_unpromoted()
    {
        using var client = new ScriptedChatClient();

        var result = await LearningAdaptationWorkflow.RunAsync(
            client,
            "Initial prompt from the caller.",
            maxIterations: 1);

        Assert.Equal(0, result.Baseline.PassedCases);
        Assert.Equal(3, result.Best.PassedCases);
        Assert.True(result.Improved);
        Assert.Equal(ProposedInstructions, result.Best.Instructions);
        Assert.Single(result.Proposals);
        Assert.Equal(7, client.Requests.Count);
        Assert.Equal("Initial prompt from the caller.", client.Requests[0].Instructions);
        Assert.Contains("required phrases missing", client.Requests[3].Input, StringComparison.OrdinalIgnoreCase);
        Assert.All(client.Requests.Skip(4), request => Assert.Equal(ProposedInstructions, request.Instructions));
        Assert.Contains("Best-scoring prompt:", result.Format());
    }

    [Fact]
    public async Task Non_improving_candidate_is_not_promoted_and_stops_the_loop()
    {
        using var client = new ScriptedChatClient(proposalImproves: false);

        var result = await LearningAdaptationWorkflow.RunAsync(
            client,
            "Initial prompt from the caller.",
            maxIterations: 3);

        Assert.Equal("Initial prompt from the caller.", result.Best.Instructions);
        Assert.Equal(result.Baseline.PassedCases, result.Best.PassedCases);
        Assert.False(result.Improved);
        Assert.Single(result.Proposals);
        Assert.Contains("No candidate improved", result.Format());
        Assert.Equal(7, client.Requests.Count);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("valid prompt", 0)]
    [InlineData("valid prompt", 4)]
    public async Task Invalid_request_is_rejected_before_model_calls(string instructions, int iterations)
    {
        using var client = new ScriptedChatClient();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            LearningAdaptationWorkflow.RunAsync(client, instructions, iterations));

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task Responses_protocol_returns_a_request_scoped_proposal()
    {
        using var model = new ScriptedChatClient();
        using IChatClient chatClient = model.AsBuilder()
            .UseOpenTelemetry(
                sourceName: LearningAdaptationWorkflow.TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.ConfigureTracing(tracing => tracing.AddSource(LearningAdaptationWorkflow.TelemetrySourceName));
        builder.Services.AddFoundryResponses(new LearningAdaptationAgent(chatClient));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();

        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter09-learning-adaptation",
                input = "Caller-owned prompt that must remain a proposal.",
                stream = false,
                store = false
            });
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var responseText = string.Concat(
                body.RootElement.GetProperty("output").EnumerateArray()
                    .SelectMany(item => item.GetProperty("content").EnumerateArray())
                    .Where(item => item.GetProperty("type").GetString() == "output_text")
                    .Select(item => item.GetProperty("text").GetString()));

            Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
            Assert.Contains("Baseline score: 0/3", responseText);
            Assert.Contains("Best-scoring prompt:", responseText);
            Assert.Equal(7, model.Requests.Count);
            Assert.Contains(model.Requests, request =>
                request.Input.Contains("Caller-owned prompt that must remain a proposal.", StringComparison.Ordinal));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed record ModelRequest(string Instructions, string Input);

    private sealed class ScriptedChatClient(bool proposalImproves = true) : IChatClient
    {
        private readonly ConcurrentQueue<ModelRequest> _requests = new();

        public IReadOnlyList<ModelRequest> Requests => _requests.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageList = messages.ToList();
            var input = messageList.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            var instructions = options?.Instructions
                ?? string.Join('\n', messageList.Where(message => message.Role == ChatRole.System).Select(message => message.Text));
            _requests.Enqueue(new ModelRequest(instructions, input));

            string output;
            if (instructions.Contains("You improve task instructions", StringComparison.Ordinal))
            {
                output = ProposedInstructions;
            }
            else if (!proposalImproves && instructions == ProposedInstructions)
            {
                output = "Please contact customer support for help.";
            }
            else if (instructions == "Initial prompt from the caller." ||
                     instructions == "Caller-owned prompt that must remain a proposal.")
            {
                output = "Please contact customer support for help.";
            }
            else if (input.Contains("arrived four days ago", StringComparison.OrdinalIgnoreCase))
            {
                output = "The return is eligible. A refund takes five business days after receipt.";
            }
            else if (input.Contains("45 days ago", StringComparison.OrdinalIgnoreCase))
            {
                output = "It is not eligible because it is outside the 30 days.";
            }
            else
            {
                output = "Please provide your order number and a photo of the damage.";
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, output)));
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
