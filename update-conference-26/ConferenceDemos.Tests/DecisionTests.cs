using System.Text.Json;
using System.Text.Json.Nodes;
using ConferenceDemos;
using ElBruno.AI.Decisions;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class DecisionTests
{
    [Fact]
    public async Task All_three_shapes_use_exactly_the_same_session_and_interest()
    {
        var context = DecisionDemo.Context();
        var client = new ScriptedDecisions(context);
        var result = await DecisionDemo.EvaluateAsync(client, context);
        Assert.Equal(["choice", "noul", "score"], client.Calls);
        Assert.Equal("strong-fit", result.Choice.Choice);
        Assert.Equal(0.9, result.Noul.Probability);
        Assert.Equal(2.5, result.Score.Score);
        Assert.Contains("Attendee interest:", context.State);
        Assert.Contains("Agentic AI on Microsoft Foundry", context.State);
    }

    [Fact]
    public async Task Capture_roundtrip_keeps_all_results_metadata_and_input_contract()
    {
        var context = DecisionDemo.Context();
        var result = await DecisionDemo.EvaluateAsync(new ScriptedDecisions(context), context);
        var capture = DecisionDemo.Capture(result, context, "fixture-openai-deployment");
        DecisionDemo.ValidateCapture(capture, context);
        var path = Path.Combine(Path.GetTempPath(), $"conference-capture-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(capture, DecisionDemo.JsonOptions));
            var read = DecisionDemo.ReadCapture(path, context);
            Assert.Equal("Azure OpenAI Decisions", read.Provider);
            Assert.Equal("fixture-openai-deployment", read.ModelDeployment);
            Assert.Equal(capture.ContextHash, read.ContextHash);
            Assert.Equal(0.9, read.Results.GetProperty("Noul").GetProperty("Probability").GetDouble());
            Assert.Equal(2.5, read.Results.GetProperty("Score").GetProperty("Score").GetDouble());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Captures_cannot_silently_change_context_provider_version_or_timestamp()
    {
        var context = DecisionDemo.Context();
        var result = await DecisionDemo.EvaluateAsync(new ScriptedDecisions(context), context);
        var capture = DecisionDemo.Capture(result, context, "fixture-deployment");
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { FormatVersion = 2 }, context));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { Provider = "Microsoft" }, context));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { ModelDeployment = "" }, context));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { CapturedAt = default }, context));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { CapturedAt = DateTimeOffset.UtcNow.AddDays(1) }, context));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { ContextHash = "fabricated" }, context));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture, context with { State = "different input" }));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { Context = context with { NoulQuestion = "different question" } }, context));
        Assert.Throws<FileNotFoundException>(() => DecisionDemo.ReadCapture(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), context));
    }

    [Theory]
    [InlineData("choice")]
    [InlineData("noul")]
    [InlineData("score")]
    [InlineData("scale")]
    public async Task Malformed_results_are_not_accepted_as_verified_captures(string corruption)
    {
        var context = DecisionDemo.Context();
        var result = await DecisionDemo.EvaluateAsync(new ScriptedDecisions(context), context);
        var json = JsonSerializer.SerializeToNode(result)!.AsObject();
        switch (corruption)
        {
            case "choice": json["Choice"]!["Choice"] = "invented"; break;
            case "noul": json["Noul"]!["Probability"] = 1.5; break;
            case "score": json["Score"]!["Score"] = 99; break;
            case "scale": json["Score"]!["LevelProbabilities"] = new JsonArray(1.0, 0.0); break;
        }
        Assert.Throws<InvalidOperationException>(() =>
            DecisionDemo.ValidateResults(JsonSerializer.SerializeToElement(json), context));
    }

    [Fact]
    public async Task Provider_failure_and_cancellation_propagate_without_capture()
    {
        var context = DecisionDemo.Context();
        await Assert.ThrowsAsync<DecisionException>(() =>
            DecisionDemo.EvaluateAsync(new ScriptedDecisions(context, fail: true), context));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DecisionDemo.EvaluateAsync(new ScriptedDecisions(context), context, new CancellationToken(true)));
    }

    private sealed class ScriptedDecisions(DecisionContext context, bool fail = false) : IDecisionClient
    {
        public List<string> Calls { get; } = [];

        public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
        {
            Check(input, cancellationToken);
            if (fail)
            {
                throw new DecisionException("Synthetic failure.");
            }
            Assert.Equal(context.ChoiceQuestion, instructions);
            Assert.Equal(context.Choices, options);
            Calls.Add("choice");
            return Task.FromResult(new ChoiceDecision("strong-fit", new Dictionary<string, double>
            {
                ["strong-fit"] = 0.9, ["partial-fit"] = 0.1, ["not-a-fit"] = 0
            }));
        }

        public Task<AssessmentDecision> AssessAsync(string input, string proposition, CancellationToken cancellationToken = default)
        {
            Check(input, cancellationToken);
            Assert.Equal(context.NoulQuestion, proposition);
            Calls.Add("noul");
            return Task.FromResult(new AssessmentDecision(0.9));
        }

        public Task<ScoreDecision> ScoreAsync(string input, string instructions,
            IReadOnlyList<string> rubric, CancellationToken cancellationToken = default)
        {
            Check(input, cancellationToken);
            Assert.Equal(context.ScoreQuestion, instructions);
            Assert.Equal(context.Scale, rubric);
            Calls.Add("score");
            return Task.FromResult(new ScoreDecision([0, 0, 0.5, 0.5]));
        }

        private void Check(string input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal(context.State, input);
        }
    }
}
