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
        var state = DecisionDemo.State();
        var client = new ScriptedDecisions(state);
        var result = await DecisionDemo.EvaluateAsync(client, state);
        Assert.Equal(["choice", "noul", "score"], client.Calls);
        Assert.Equal("strong-fit", result.Choice.Choice);
        Assert.Equal(0.9, result.Noul.Probability);
        Assert.Equal(2.5, result.Score.Score);
        Assert.Contains("Attendee interest:", state);
        Assert.Contains("Agentic AI on Microsoft Foundry", state);
    }

    [Fact]
    public async Task Capture_roundtrip_keeps_all_results_metadata_and_input_contract()
    {
        var state = DecisionDemo.State();
        var result = await DecisionDemo.EvaluateAsync(new ScriptedDecisions(state), state);
        var capture = DecisionDemo.Capture(result, state, "fixture-openai-deployment");
        DecisionDemo.ValidateCapture(capture, state);
        var path = Path.Combine(Path.GetTempPath(), $"conference-capture-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(capture, DecisionDemo.JsonOptions));
            var read = DecisionDemo.ReadCapture(path, state);
            Assert.Equal("Azure OpenAI Decisions", read.Provider);
            Assert.Equal("fixture-openai-deployment", read.ModelDeployment);
            Assert.Equal(capture.State, read.State);
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
        var state = DecisionDemo.State();
        var result = await DecisionDemo.EvaluateAsync(new ScriptedDecisions(state), state);
        var capture = DecisionDemo.Capture(result, state, "fixture-deployment");
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { FormatVersion = 1 }, state));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { Provider = "Microsoft" }, state));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { ModelDeployment = "" }, state));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { CapturedAt = null }, state));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { CapturedAt = DateTimeOffset.MinValue }, state));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { CapturedAt = DateTimeOffset.UtcNow.AddDays(1) }, state));
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateCapture(capture with { State = "different input" }, state));
        Assert.Throws<FileNotFoundException>(() => DecisionDemo.ReadCapture(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), state));
    }

    [Fact]
    public async Task Bundled_sample_contains_all_three_shapes_without_claiming_provider_provenance()
    {
        var state = DecisionDemo.State();
        var sample = DecisionDemo.ReadCapture(DecisionDemo.SamplePath, state);
        Assert.Equal(DecisionDemo.SampleProvider, sample.Provider);
        Assert.Null(sample.ModelDeployment);
        Assert.Null(sample.CapturedAt);
        Assert.Throws<InvalidOperationException>(() =>
            DecisionDemo.ValidateCapture(sample with { Provider = "Azure OpenAI Decisions" }, state));
        Assert.Throws<InvalidOperationException>(() =>
            DecisionDemo.ValidateCapture(sample with { ModelDeployment = "invented" }, state));
        Assert.Throws<InvalidOperationException>(() =>
            DecisionDemo.ValidateCapture(sample with { CapturedAt = DateTimeOffset.UtcNow }, state));

        var expected = await DecisionDemo.EvaluateAsync(new ScriptedDecisions(state), state);
        Assert.Equal(JsonSerializer.SerializeToElement(expected).GetProperty("Choice").GetProperty("Choice").GetString(),
            sample.Results.GetProperty("Choice").GetProperty("Choice").GetString());
        Assert.Equal(expected.Noul.Probability, sample.Results.GetProperty("Noul").GetProperty("Probability").GetDouble());
        Assert.Equal(expected.Score.Score, sample.Results.GetProperty("Score").GetProperty("Score").GetDouble());
        Assert.Equal(0, await Program.Main(["check-data"]));
    }

    [Fact]
    public async Task Missing_capture_and_invalid_arguments_fail_before_live_inference()
    {
        Assert.Equal(1, await Program.Main(["decisions", Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")]));
        Assert.Equal(1, await Program.Main(["decisions-record-openai"]));
        Assert.Equal(1, await Program.Main(["decisions", "one.json", "two.json"]));
    }

    [Theory]
    [InlineData("choice")]
    [InlineData("noul")]
    [InlineData("score")]
    [InlineData("scale")]
    public async Task Malformed_results_are_not_accepted_as_verified_captures(string corruption)
    {
        var state = DecisionDemo.State();
        var result = await DecisionDemo.EvaluateAsync(new ScriptedDecisions(state), state);
        var json = JsonSerializer.SerializeToNode(result)!.AsObject();
        switch (corruption)
        {
            case "choice": json["Choice"]!["Choice"] = "invented"; break;
            case "noul": json["Noul"]!["Probability"] = 1.5; break;
            case "score": json["Score"]!["Score"] = 99; break;
            case "scale": json["Score"]!["LevelProbabilities"] = new JsonArray(1.0, 0.0); break;
        }
        Assert.Throws<InvalidOperationException>(() =>
            DecisionDemo.ValidateResults(JsonSerializer.SerializeToElement(json)));
    }

    [Fact]
    public async Task Provider_failure_and_cancellation_propagate_without_capture()
    {
        var state = DecisionDemo.State();
        await Assert.ThrowsAsync<DecisionException>(() =>
            DecisionDemo.EvaluateAsync(new ScriptedDecisions(state, fail: true), state));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DecisionDemo.EvaluateAsync(new ScriptedDecisions(state), state, new CancellationToken(true)));
    }

    private sealed class ScriptedDecisions(string state, bool fail = false) : IDecisionClient
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
            Assert.Equal("Which relevance label best fits the attendee's stated interest?", instructions);
            Assert.Equal(new Dictionary<string, string?>
            {
                ["strong-fit"] = "Directly relevant to practical C# agent development and Foundry.",
                ["partial-fit"] = "Some relevant ideas but not primarily this interest.",
                ["not-a-fit"] = "Not relevant to the stated interest."
            }, options);
            Calls.Add("choice");
            return Task.FromResult(new ChoiceDecision("strong-fit", new Dictionary<string, double>
            {
                ["strong-fit"] = 0.9, ["partial-fit"] = 0.1, ["not-a-fit"] = 0
            }));
        }

        public Task<AssessmentDecision> AssessAsync(string input, string proposition, CancellationToken cancellationToken = default)
        {
            Check(input, cancellationToken);
            Assert.Equal("Is this session relevant to practical AI agents in C# and Microsoft Foundry?", proposition);
            Calls.Add("noul");
            return Task.FromResult(new AssessmentDecision(0.9));
        }

        public Task<ScoreDecision> ScoreAsync(string input, string instructions,
            IReadOnlyList<string> rubric, CancellationToken cancellationToken = default)
        {
            Check(input, cancellationToken);
            Assert.Equal("How relevant is this abstract to the stated attendee interest on this ordered scale?", instructions);
            Assert.Equal(["Not relevant", "Slightly relevant", "Relevant", "Very relevant"], rubric);
            Calls.Add("score");
            return Task.FromResult(new ScoreDecision([0, 0, 0.5, 0.5]));
        }

        private void Check(string input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal(state, input);
        }
    }
}
