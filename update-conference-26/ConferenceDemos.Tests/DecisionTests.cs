using System.Text.Json;
using System.Text.Json.Nodes;
using ConferenceDemos;
using ElBruno.AI.Decisions;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class DecisionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Calls_are_live_sequential_and_share_exact_tasks_and_state(bool compare)
    {
        var state = DecisionDemo.State();
        var calls = new List<string>();
        var messages = new List<string>();
        var microsoft = new ScriptedDecisions(state, "Microsoft", calls);
        var openai = compare ? new ScriptedDecisions(state, "OpenAI", calls) : null;
        var result = await DecisionDemo.EvaluateAsync(microsoft, state, showTask: messages.Add, openai: openai);
        string[] tasks = ["day1", "day2", "choice", "score6", "score18", "score20"];
        Assert.Equal(tasks.SelectMany(task => compare
            ? new[] { $"Microsoft:{task}", $"OpenAI:{task}" } : new[] { $"Microsoft:{task}" }), calls);
        Assert.EndsWith(ScheduleCorpus.LoadMarkdown().ReplaceLineEndings("\n"), state);
        Assert.Contains("Cédric Mendelin", DecisionDemo.SessionChoices()["s13"]);
        Assert.Equal(11, DecisionDemo.SessionChoices().Count);
        Assert.Equal("s17", result.Choice.Choice);
        Assert.Equal(["s6", "s18", "s20"], result.Scores.Keys);
        Assert.Equal(compare ? 30 : 18, messages.Count);
        var stride = compare ? 5 : 3;
        for (var i = 0; i < messages.Count; i += stride)
        {
            Assert.Contains("Question:", messages[i]);
            Assert.Contains("State: complete two-day schedule", messages[i]);
            Assert.Contains("Microsoft-Decision-1 - LIVE", messages[i + 1]);
            Assert.Contains("RUNNING", messages[i + 1]);
            Assert.StartsWith("API call: COMPLETED", messages[i + 2]);
            if (compare)
            {
                Assert.Contains("Azure OpenAI Decisions - LIVE", messages[i + 3]);
                Assert.StartsWith("API call: COMPLETED", messages[i + 4]);
            }
        }
        Assert.All(messages, message =>
        {
            Assert.DoesNotContain("Replay", message);
            Assert.DoesNotContain("\"Usage\"", message);
            Assert.DoesNotContain("### At a glance", message);
        });
    }

    [Fact]
    public async Task Obsolete_paths_and_invalid_flags_fail_before_inference()
    {
        Assert.Equal(1, await Program.Main(["decisions", "capture.json"]));
        Assert.Equal(1, await Program.Main(["decisions", "--compare-openai", "extra"]));
        Assert.Equal(1, await Program.Main(["decisions-record-openai"]));
    }

    [Theory]
    [InlineData("choice")]
    [InlineData("day1")]
    [InlineData("day2")]
    [InlineData("missing-choice")]
    [InlineData("extra-choice")]
    [InlineData("score")]
    [InlineData("scale")]
    [InlineData("missing-score")]
    [InlineData("extra-score")]
    public async Task Malformed_results_are_rejected(string corruption)
    {
        var state = DecisionDemo.State();
        var result = await DecisionDemo.EvaluateAsync(new ScriptedDecisions(state, "Microsoft", []), state);
        var json = JsonSerializer.SerializeToNode(result)!.AsObject();
        switch (corruption)
        {
            case "choice": json["Choice"]!["Choice"] = "invented"; break;
            case "day1": json["Day1"]!["Probability"] = 1.5; break;
            case "day2": json["Day2"]!["Probability"] = -0.1; break;
            case "missing-choice": json["Choice"]!["Probabilities"]!.AsObject().Remove("s46"); break;
            case "extra-choice": json["Choice"]!["Probabilities"]!["s99"] = 0; break;
            case "score": json["Scores"]!["s18"]!["Score"] = 99; break;
            case "scale": json["Scores"]!["s20"]!["LevelProbabilities"] = new JsonArray(1.0, 0.0); break;
            case "missing-score": json["Scores"]!.AsObject().Remove("s6"); break;
            case "extra-score": json["Scores"]!["s99"] = json["Scores"]!["s6"]!.DeepClone(); break;
        }
        Assert.Throws<InvalidOperationException>(() => DecisionDemo.ValidateResults(JsonSerializer.SerializeToElement(json)));
    }

    [Fact]
    public async Task Comparison_failure_and_cancellation_propagate()
    {
        var state = DecisionDemo.State();
        var calls = new List<string>();
        await Assert.ThrowsAsync<DecisionException>(() => DecisionDemo.EvaluateAsync(
            new ScriptedDecisions(state, "Microsoft", calls), state,
            openai: new ScriptedDecisions(state, "OpenAI", calls, fail: true)));
        Assert.Equal(["Microsoft:day1"], calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DecisionDemo.EvaluateAsync(
            new ScriptedDecisions(state, "Microsoft", []), state, new CancellationToken(true)));
    }

    private sealed class ScriptedDecisions(string state, string provider, List<string> calls, bool fail = false) : IDecisionClient
    {
        private int assessmentCount;
        private int scoreCount;

        public Task<AssessmentDecision> AssessAsync(string input, string proposition, CancellationToken cancellationToken = default)
        {
            Check(input, cancellationToken);
            var day = ++assessmentCount;
            Assert.Equal($"Is Cédric Mendelin speaking on Day {day} of the conference?", proposition);
            calls.Add($"{provider}:day{day}");
            return Task.FromResult(new AssessmentDecision(day == 1 ? 0.95 : 0.05));
        }

        public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default)
        {
            Check(input, cancellationToken);
            Assert.Equal(DecisionDemo.ChoiceQuestion, instructions);
            int[] ids = [5, 9, 13, 17, 21, 22, 30, 32, 34, 43, 46];
            Assert.Equal(ids.Select(id => $"s{id}"), options.Keys);
            var corpus = new ScheduleCorpus();
            foreach (var id in ids)
            {
                var session = corpus.Sessions.Single(s => s.Id == id);
                Assert.Equal($"{session.Speaker} - {session.Title}", options[$"s{id}"]);
            }
            calls.Add($"{provider}:choice");
            return Task.FromResult(new ChoiceDecision("s17", ids.ToDictionary(
                id => $"s{id}", id => id == 17 ? 0.6 : id == 5 ? 0.2 : id == 13 ? 0.2 : 0)));
        }

        public Task<ScoreDecision> ScoreAsync(string input, string instructions,
            IReadOnlyList<string> rubric, CancellationToken cancellationToken = default)
        {
            Check(input, cancellationToken);
            int[] ids = [6, 18, 20];
            var id = ids[scoreCount++];
            Assert.Equal(DecisionDemo.ScoreQuestion(id), instructions);
            Assert.Equal(DecisionDemo.ScoreRubric, rubric);
            calls.Add($"{provider}:score{id}");
            return Task.FromResult(new ScoreDecision([0.9, 0.1, 0, 0]));
        }

        private void Check(string input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal(state, input);
            if (fail) throw new DecisionException("Synthetic provider failure.");
        }
    }
}
