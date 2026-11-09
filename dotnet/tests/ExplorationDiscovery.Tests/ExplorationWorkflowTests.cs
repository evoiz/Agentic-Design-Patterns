using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ExplorationDiscovery;
using ExplorationDiscovery.Hosted;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.ExplorationDiscovery.Tests;

public sealed class ExplorationWorkflowTests
{
    [Fact]
    public async Task Stops_when_all_reviewers_accept_the_proposal()
    {
        using var client = new ScriptedChatClient(
            ProposalJson("Initial research plan."),
            ReviewJson("Accept", 8),
            ReviewJson("Accept", 9),
            ReviewJson("Accept", 8));

        var result = await ExplorationWorkflow.RunAsync(client, "A bounded test question.");

        Assert.True(result.Accepted);
        Assert.Equal(1, result.ReviewRounds);
        Assert.Equal(3, result.Reviews.Count);
        Assert.Equal(4, client.Requests.Count);
        Assert.Contains("unverified proposal", result.Format(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1. Define baseline.", result.Format());
        Assert.DoesNotContain("1. 1.", result.Format());
    }

    [Fact]
    public async Task Runs_the_three_reviewer_calls_concurrently()
    {
        var allReviewersStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reviewerCalls = 0;
        var activeReviewers = 0;
        var maximumActiveReviewers = 0;
        using var client = new CallbackChatClient(async (call, cancellationToken) =>
        {
            if (call == 1)
            {
                return ProposalJson("Concurrent review plan.");
            }

            var active = Interlocked.Increment(ref activeReviewers);
            UpdateMaximum(ref maximumActiveReviewers, active);
            if (Interlocked.Increment(ref reviewerCalls) == 3)
            {
                allReviewersStarted.TrySetResult();
            }

            try
            {
                await allReviewersStarted.Task.WaitAsync(cancellationToken);
                return ReviewJson("Accept", 8);
            }
            finally
            {
                Interlocked.Decrement(ref activeReviewers);
            }
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await ExplorationWorkflow.RunAsync(
            client, "A bounded test question.", cancellationToken: timeout.Token);

        Assert.True(result.Accepted);
        Assert.Equal(3, maximumActiveReviewers);
        Assert.Equal(4, client.CallCount);
    }

    [Fact]
    public async Task Caller_cancellation_stops_the_review_panel()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new CallbackChatClient((call, token) =>
        {
            if (call == 1)
            {
                return Task.FromResult(ProposalJson("Cancelable review plan."));
            }

            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(ReviewJson("Accept", 8));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ExplorationWorkflow.RunAsync(
                client, "A bounded test question.", cancellationToken: cancellation.Token));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(4, client.CallCount);
    }

    [Fact]
    public async Task Preserves_decimal_prefixes_while_removing_list_numbering()
    {
        using var client = new ScriptedChatClient(
            ProposalJson(
                "A decimal-prefix test.",
                ["1. Define baseline.", "1.5x load: measure p99 latency.", "99.9% confidence interval."]),
            ReviewJson("Accept", 8),
            ReviewJson("Accept", 9),
            ReviewJson("Accept", 8));

        var result = await ExplorationWorkflow.RunAsync(client, "A bounded test question.");

        Assert.Equal(
            ["Define baseline.", "1.5x load: measure p99 latency.", "99.9% confidence interval."],
            result.Proposal.Plan);
        Assert.Contains("1.5x load: measure p99 latency.", client.Requests[1].Input);
        Assert.Contains("99.9% confidence interval.", client.Requests[1].Input);
    }

    [Fact]
    public async Task Revises_once_then_returns_the_best_proposal_when_reviewers_reject()
    {
        using var client = new ScriptedChatClient(
            ProposalJson("First plan."),
            ReviewJson("Reject", 5),
            ReviewJson("Reject", 6),
            ReviewJson("Reject", 5),
            ProposalJson("Revised plan."),
            ReviewJson("Reject", 6),
            ReviewJson("Accept", 8),
            ReviewJson("Reject", 6));

        var result = await ExplorationWorkflow.RunAsync(client, "A bounded test question.");

        Assert.False(result.Accepted);
        Assert.Equal(ExplorationWorkflow.MaxReviewRounds, result.ReviewRounds);
        Assert.Equal("Revised plan.", result.Proposal.ResearchQuestion);
        Assert.Equal(8, client.Requests.Count);
    }

    [Fact]
    public async Task Rejects_an_invalid_proposal_before_calling_reviewers()
    {
        using var client = new ScriptedChatClient(
            JsonSerializer.Serialize(new
            {
                researchQuestion = "A test question.",
                plan = new[] { "Only one step." },
                evidenceNeeds = new[] { "Evidence." },
                risks = new[] { "Risk." }
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExplorationWorkflow.RunAsync(client, "A bounded test question."));

        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task Requires_ethical_concerns_to_be_resolved_before_acceptance()
    {
        using var client = new ScriptedChatClient(
            ProposalJson("First research plan."),
            ReviewJson("Accept", 8),
            ReviewJson("Accept", 8, ethicalConcerns: true),
            ReviewJson("Accept", 8),
            ProposalJson("Revised research plan."),
            ReviewJson("Accept", 8),
            ReviewJson("Accept", 8),
            ReviewJson("Accept", 8));

        var result = await ExplorationWorkflow.RunAsync(client, "A bounded test question.");

        Assert.True(result.Accepted);
        Assert.Equal(2, result.ReviewRounds);
        Assert.Equal(8, client.Requests.Count);
    }

    [Fact]
    public async Task Rejects_topics_over_the_character_budget_without_model_calls()
    {
        using var client = new ScriptedChatClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ExplorationWorkflow.RunAsync(client, new string('x', ExplorationWorkflow.MaxTopicLength + 1)));

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task Hosted_stream_returns_one_completed_reviewed_proposal_for_the_caller()
    {
        using var client = new ScriptedChatClient(
            ProposalJson("Caller-specific research plan."),
            ReviewJson("Accept", 8), ReviewJson("Accept", 9), ReviewJson("Accept", 8));
        var updates = new List<string>();
        await foreach (var update in new ExplorationHostedAgent(client).RunStreamingAsync("Compare request batching."))
            updates.Add(update.Text);
        Assert.Contains("Caller-specific research plan.", Assert.Single(updates));
        Assert.Contains("Compare request batching.", client.Requests[0].Input);
        Assert.Equal(4, client.Requests.Count);
    }

    [Theory]
    [InlineData("missing-ethics")]
    [InlineData("invalid-score")]
    [InlineData("blank-list-item")]
    public async Task Rejects_incomplete_or_invalid_reviewer_assessments(string invalid)
    {
        var review = ReviewJson("Accept", invalid == "invalid-score" ? 11 : 8);
        if (invalid == "missing-ethics") review = review.Replace("\"ethicalConcerns\":false,", "");
        if (invalid == "blank-list-item") review = review.Replace("Clear comparison.", " ");
        using var client = new ScriptedChatClient(
            ProposalJson("Test plan."), review, ReviewJson("Accept", 8), ReviewJson("Accept", 8));
        if (invalid == "missing-ethics")
            await Assert.ThrowsAsync<JsonException>(() => ExplorationWorkflow.RunAsync(client, "A bounded test question."));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => ExplorationWorkflow.RunAsync(client, "A bounded test question."));
        Assert.InRange(client.Requests.Count, 2, 4);
    }

    private static string ProposalJson(string question, IReadOnlyList<string>? plan = null) =>
        JsonSerializer.Serialize(new
        {
            researchQuestion = question,
            plan = plan ?? ["1. Define baseline.", "2. Collect measurements.", "3. Compare outcomes."],
            evidenceNeeds = new[] { "Latency measurements." },
            risks = new[] { "Results may not generalize." }
        });

    private static string ReviewJson(string decision, int overall, bool ethicalConcerns = false) =>
        JsonSerializer.Serialize(new
        {
            summary = "The plan is testable.",
            strengths = new[] { "Clear comparison." },
            weaknesses = new[] { "Small scope." },
            questions = new[] { "Which workloads?" },
            limitations = new[] { "Synthetic data may differ." },
            ethicalConcerns,
            overall,
            confidence = 4,
            decision
        });

    private static void UpdateMaximum(ref int maximum, int value)
    {
        var observed = Volatile.Read(ref maximum);
        while (value > observed)
        {
            var previous = Interlocked.CompareExchange(ref maximum, value, observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }

    private sealed class CallbackChatClient(
        Func<int, CancellationToken, Task<string>> respond) : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await respond(Interlocked.Increment(ref _callCount), cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
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
