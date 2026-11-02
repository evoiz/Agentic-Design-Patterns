using System.Collections.Concurrent;
using AgenticDesignPatterns.TestSupport;
using Chapter09.LearningAdaptation;
using Chapter09.LearningAdaptation.Hosted;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.LearningAdaptation.Tests;

public sealed class LearningAdaptationAgentTests
{
    private const string ProposedInstructions =
        "Use only supplied policies. State return eligibility and timing, and request an order number and damage photo when needed.";
    private const string CallerPrompt = "Initial prompt from the caller.";

    [Fact]
    public async Task Passing_baseline_skips_curation_and_reports_no_update()
    {
        var model = new BenchmarkModel();

        var result = await LearningAdaptationWorkflow.RunAsync(model.Client, ProposedInstructions);

        Assert.Equal(3, result.Baseline.PassedCases);
        Assert.Empty(result.Proposals);
        Assert.Equal(3, model.Requests.Count);
        Assert.Contains("The starting prompt already passes the benchmark; no update is recommended.", result.Format());
    }

    [Fact]
    public async Task Adaptation_uses_deterministic_benchmark_and_keeps_proposal_unpromoted()
    {
        var model = new BenchmarkModel();

        var result = await LearningAdaptationWorkflow.RunAsync(model.Client, CallerPrompt, maxIterations: 1);

        Assert.Equal(0, result.Baseline.PassedCases);
        Assert.Equal(3, result.Best.PassedCases);
        Assert.True(result.Improved);
        Assert.Equal(ProposedInstructions, result.Best.Instructions);
        Assert.Single(result.Proposals);
        Assert.Equal(7, model.Requests.Count);
        Assert.Equal(CallerPrompt, model.Requests[0].Instructions);
        Assert.Contains("required phrases missing", model.Requests[3].Input, StringComparison.OrdinalIgnoreCase);
        Assert.All(model.Requests.Skip(4), request => Assert.Equal(ProposedInstructions, request.Instructions));
        Assert.Contains("Best-scoring prompt:", result.Format());
    }

    [Fact]
    public async Task Non_improving_candidate_is_not_promoted_and_stops_the_loop()
    {
        var model = new BenchmarkModel(proposalImproves: false);

        var result = await LearningAdaptationWorkflow.RunAsync(model.Client, CallerPrompt, maxIterations: 3);

        Assert.Equal(CallerPrompt, result.Best.Instructions);
        Assert.False(result.Improved);
        Assert.Single(result.Proposals);
        Assert.Contains("No candidate improved", result.Format());
        Assert.Equal(7, model.Requests.Count);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("valid prompt", 0)]
    [InlineData("valid prompt", 4)]
    public async Task Invalid_request_is_rejected_before_model_calls(string instructions, int iterations)
    {
        var model = new BenchmarkModel();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            LearningAdaptationWorkflow.RunAsync(model.Client, instructions, iterations));

        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Hosted_responses_smoke_returns_a_request_scoped_proposal()
    {
        var model = new BenchmarkModel();
        await using var host = await HostedResponses.StartAsync(new LearningAdaptationAgent(model.Client));
        using var body = await host.InvokeAsync(CallerPrompt, "chapter09-learning-adaptation");
        var responseText = HostedResponses.OutputText(body.RootElement);

        Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
        Assert.Contains("Baseline score: 0/3", responseText);
        Assert.Contains("Best-scoring prompt:", responseText);
        Assert.Equal(CallerPrompt, model.Requests[0].Instructions);
    }
    private sealed record ModelRequest(string Instructions, string Input);

    // Answers benchmark cases correctly only under the curated prompt.
    private sealed class BenchmarkModel
    {
        private readonly ConcurrentQueue<ModelRequest> _requests = new();

        public BenchmarkModel(bool proposalImproves = true) =>
            Client = new ScriptedChatClient((messages, options) =>
            {
                var messageList = messages.ToList();
                var input = messageList.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
                var instructions = options?.Instructions ?? string.Join(
                    '\n', messageList.Where(message => message.Role == ChatRole.System).Select(message => message.Text));
                _requests.Enqueue(new ModelRequest(instructions, input));
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer(instructions, input, proposalImproves)));
            });

        public IChatClient Client { get; }
        public IReadOnlyList<ModelRequest> Requests => _requests.ToArray();

        private static string Answer(string instructions, string input, bool proposalImproves) =>
            instructions.Contains("You improve task instructions", StringComparison.Ordinal) ? ProposedInstructions
            : instructions == CallerPrompt || (!proposalImproves && instructions == ProposedInstructions)
                ? "Please contact customer support for help."
            : input.Contains("arrived four days ago", StringComparison.OrdinalIgnoreCase)
                ? "The return is eligible. A refund takes five business days after receipt."
            : input.Contains("45 days ago", StringComparison.OrdinalIgnoreCase)
                ? "It is not eligible because it is outside the 30 days."
            : "Please provide your order number and a photo of the damage.";
    }
}
