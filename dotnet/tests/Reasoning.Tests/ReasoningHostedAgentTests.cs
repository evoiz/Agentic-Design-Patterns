using System.Collections.Concurrent;
using AgenticDesignPatterns.TestSupport;
using System.Globalization;
using System.Runtime.CompilerServices;
using Reasoning;
using Reasoning.Hosted;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.Reasoning.Tests;

public sealed class ReasoningHostedAgentTests
{
    [Theory]
    [InlineData("cot: Explain quantum computing.", ReasoningScenario.ChainOfThought, "Explain quantum computing.")]
    [InlineData("reason: Explain quantum computing.", ReasoningScenario.ChainOfThought, "Explain quantum computing.")]
    [InlineData("self-correction: Improve this post.", ReasoningScenario.SelfCorrection, "Improve this post.")]
    [InlineData("react: Calculate 5 factorial.", ReasoningScenario.ReAct, "Calculate 5 factorial.")]
    [InlineData("deep-search: Compare two ideas.", ReasoningScenario.DeepSearch, "Compare two ideas.")]
    [InlineData("Explain quantum computing.", ReasoningScenario.ChainOfThought, "Explain quantum computing.")]
    public void Request_parser_routes_known_modes_and_preserves_caller_input(
        string request,
        ReasoningScenario expectedScenario,
        string expectedInput)
    {
        var parsed = ReasoningHostedAgent.ParseRequest(request);

        Assert.Equal(expectedScenario, parsed.Scenario);
        Assert.Equal(expectedInput, parsed.Input);
    }

    [Theory]
    [InlineData("cot: ")]
    [InlineData("react: ")]
    [InlineData(" ")]
    public void Request_parser_rejects_empty_input(string request) =>
        Assert.Throws<ArgumentException>(() => ReasoningHostedAgent.ParseRequest(request));

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "1")]
    [InlineData(10, "3628800")]
    [InlineData(20, "2432902008176640000")]
    public void Factorial_tool_returns_exact_results(int value, string expected) =>
        Assert.Equal(expected, ReasoningWorkflow.CalculateFactorial(value));

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void Factorial_tool_rejects_values_outside_its_safe_range(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ReasoningWorkflow.CalculateFactorial(value));

    [Fact]
    public async Task Self_correction_stops_after_the_fixed_round_budget_without_accepting()
    {
        using var model = new SequencedChatClient(
            "Draft one.",
            """{"Accept":false,"Feedback":"Revise the draft."}""",
            "Draft two.",
            """{"Accept":false,"Feedback":"Still needs revision."}""",
            "Draft three.",
            """{"Accept":false,"Feedback":"The limit was reached."}""");

        var result = await ReasoningWorkflow.RunAsync(
            model, ReasoningScenario.SelfCorrection, "Write a short post.");

        Assert.Contains("Outcome: MaxRoundsReached", result);
        Assert.Contains("The draft was not accepted within the round limit.", result);
        Assert.Equal(ReasoningWorkflow.MaxCorrectionRounds * 2, model.Requests.Count);
    }

    [Fact]
    public async Task Deep_search_rejects_follow_up_queries_above_the_per_round_limit()
    {
        using var model = new SequencedChatClient(
            """{"Queries":["initial question"]}""",
            "Background note.",
            """{"SufficientCoverage":false,"FollowUpQueries":["one","two","three","four"],"Summary":"More coverage is needed."}""");

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            ReasoningWorkflow.RunAsync(model, ReasoningScenario.DeepSearch, "A research question."));

        Assert.Contains($"between one and {ReasoningWorkflow.MaxQueriesPerRound}", exception.ToString());
        Assert.Equal(3, model.Requests.Count);
    }

    [Fact]
    public async Task Self_correction_uses_caller_requirements_instead_of_sample_specific_instructions()
    {
        using var model = new SelfCorrectionChatClient();
        const string requirements = "Write a concise announcement for BlueSky Coffee's new seasonal menu.";

        var result = await ReasoningWorkflow.RunAsync(
            model,
            ReasoningScenario.SelfCorrection,
            requirements);

        Assert.Contains("BlueSky Coffee", result, StringComparison.Ordinal);
        Assert.Contains(model.Requests, request => request.Contains(requirements, StringComparison.Ordinal));
        Assert.DoesNotContain(
            model.Requests.SelectMany(request => request.Split('\n')),
            line => line.Contains("GreenTech Gadgets", StringComparison.Ordinal));
        var finalDraftStart = result.IndexOf("Final draft:", StringComparison.Ordinal);
        Assert.True(finalDraftStart >= 0);
        var finalDraft = result[(finalDraftStart + "Final draft:".Length)..]
            .Split("Review:", StringSplitOptions.None)[0]
            .Trim();
        Assert.InRange(new StringInfo(finalDraft).LengthInTextElements, 1, 150);
    }

    [Fact]
    public async Task Responses_protocol_returns_result_for_the_callers_request()
    {
        using var model = new ScriptedChatClient();
        await using var host = await HostedResponses.StartAsync(new ReasoningHostedAgent(model));
        using var body = await host.InvokeAsync("cot: Explain a caller-specific topic.", "chapter17-reasoning");
        Assert.Contains("Scripted reasoning response.", HostedResponses.OutputText(body.RootElement));
        Assert.Contains("caller-specific topic", Assert.Single(model.Requests), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly ConcurrentQueue<string> _requests = new();

        public IReadOnlyList<string> Requests => _requests.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            _requests.Enqueue(input);
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "Scripted reasoning response.")));
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

    private sealed class SelfCorrectionChatClient : IChatClient
    {
        private readonly ConcurrentQueue<string> _requests = new();
        private int _requestCount;

        public IReadOnlyList<string> Requests => _requests.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = string.Join("\n", messages.Select(message => message.Text));
            _requests.Enqueue(request);
            var callNumber = Interlocked.Increment(ref _requestCount);
            var response = callNumber switch
            {
                1 => $"BlueSky Coffee {new string('x', 160)}",
                2 => """{"Accept":true,"Feedback":"The draft meets the caller's requirements."}""",
                3 => "BlueSky Coffee is serving a fresh seasonal menu. Stop by and find your new favorite.",
                _ => """{"Accept":true,"Feedback":"The revised draft meets the caller's requirements."}"""
            };
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, response)));
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

    private sealed class SequencedChatClient(params string[] responses) : IChatClient
    {
        private readonly ConcurrentQueue<string> _responses = new(responses);
        private readonly ConcurrentQueue<string> _requests = new();

        public IReadOnlyList<string> Requests => _requests.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _requests.Enqueue(messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty);
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("No scripted reasoning response remains.");
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
