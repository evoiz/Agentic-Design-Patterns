using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter21.ExplorationDiscovery;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.Chapter21.Tests;

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
            originality = 2,
            quality = 3,
            clarity = 3,
            significance = 2,
            soundness = 3,
            presentation = 3,
            contribution = 2,
            overall,
            confidence = 4,
            decision
        });

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
