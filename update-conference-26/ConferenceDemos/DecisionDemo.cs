using System.Text.Json;
using ElBruno.AI.Decisions;

namespace ConferenceDemos;

public sealed record DecisionResults(ChoiceDecision Choice, AssessmentDecision Noul, ScoreDecision Score);
public sealed record DecisionCapture(
    int FormatVersion, string Provider, string? ModelDeployment, DateTimeOffset? CapturedAt,
    string State, JsonElement Results);

public static class DecisionDemo
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public const string SampleProvider = "Illustrative fixture (not a model response)";
    public static string SamplePath => Path.Combine(AppContext.BaseDirectory, "Samples", "openai-decisions.sample.json");

    public static string State()
    {
        var session = new ScheduleCorpus().Sessions.Single(s => s.Id == 13);
        return "Attendee interest: practical AI agents in C# and Microsoft Foundry.\n" +
            $"Session: {session.Title}\nAbstract: {session.Abstract.ReplaceLineEndings("\n")}";
    }

    public static async Task<DecisionResults> EvaluateAsync(
        IDecisionClient client, string state, CancellationToken cancellationToken = default)
    {
        var choice = await client.ChooseAsync(
            state,
            "Which relevance label best fits the attendee's stated interest?",
            new Dictionary<string, string?>
            {
                ["strong-fit"] = "Directly relevant to practical C# agent development and Foundry.",
                ["partial-fit"] = "Some relevant ideas but not primarily this interest.",
                ["not-a-fit"] = "Not relevant to the stated interest."
            },
            cancellationToken);
        // Noul is the binary task; this library exposes it as AssessAsync / AssessmentDecision.
        var noul = await client.AssessAsync(
            state,
            "Is this session relevant to practical AI agents in C# and Microsoft Foundry?",
            cancellationToken);
        var score = await client.ScoreAsync(
            state,
            "How relevant is this abstract to the stated attendee interest on this ordered scale?",
            ["Not relevant", "Slightly relevant", "Relevant", "Very relevant"],
            cancellationToken);
        var result = new DecisionResults(choice, noul, score);
        ValidateResults(JsonSerializer.SerializeToElement(result));
        return result;
    }

    public static DecisionCapture Capture(DecisionResults results, string state, string model) =>
        new(2, "Azure OpenAI Decisions", model, DateTimeOffset.UtcNow, state,
            JsonSerializer.SerializeToElement(results));

    public static DecisionCapture ReadCapture(string path, string state)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "The decision JSON file is missing. Run decisions without a path to use the bundled illustrative sample, " +
                "or run decisions-record-openai <path> with a configured openaiDecision deployment.", path);
        }
        var capture = JsonSerializer.Deserialize<DecisionCapture>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("The capture file is empty.");
        ValidateCapture(capture, state);
        return capture;
    }

    public static void ValidateCapture(DecisionCapture capture, string state)
    {
        var validProvenance = capture.Provider == SampleProvider
            ? capture.ModelDeployment is null && capture.CapturedAt is null
            : capture.Provider == "Azure OpenAI Decisions" &&
                !string.IsNullOrWhiteSpace(capture.ModelDeployment) &&
                capture.CapturedAt is { } capturedAt && capturedAt != default &&
                capturedAt <= DateTimeOffset.UtcNow.AddMinutes(5);
        // Version 2 binds the inline questions, choices, and scale; bump it when those change.
        if (capture.FormatVersion != 2 || !validProvenance || capture.State != state)
        {
            throw new InvalidOperationException(
                "The decision JSON provenance or input/task contract does not match this demo. Re-record older captures.");
        }
        ValidateResults(capture.Results);
    }

    public static void ValidateResults(JsonElement results)
    {
        var choice = results.GetProperty("Choice");
        var probabilities = choice.GetProperty("Probabilities").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());
        var selected = choice.GetProperty("Choice").GetString();
        var levels = results.GetProperty("Score").GetProperty("LevelProbabilities").EnumerateArray().Select(p => p.GetDouble()).ToArray();
        var score = results.GetProperty("Score").GetProperty("Score").GetDouble();
        var noul = results.GetProperty("Noul").GetProperty("Probability").GetDouble();
        if (selected is null || !probabilities.ContainsKey(selected) ||
            !probabilities.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(["strong-fit", "partial-fit", "not-a-fit"]) ||
            !Distribution(probabilities.Values) || levels.Length != 4 || !Distribution(levels) ||
            !double.IsFinite(noul) || noul is < 0 or > 1 || !double.IsFinite(score) ||
            Math.Abs(score - levels.Select((p, index) => p * index).Sum()) > 0.001)
        {
            throw new InvalidOperationException("The provider response does not match the choice, Noul, and ordered-score contracts.");
        }
    }

    private static bool Distribution(IEnumerable<double> values) =>
        values.All(v => double.IsFinite(v) && v is >= 0 and <= 1) && Math.Abs(values.Sum() - 1) <= 0.001;
}
