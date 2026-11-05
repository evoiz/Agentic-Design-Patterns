using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElBruno.AI.Decisions;

namespace ConferenceDemos;

public sealed record DecisionContext(
    string State, IReadOnlyDictionary<string, string?> Choices, IReadOnlyList<string> Scale,
    string ChoiceQuestion, string NoulQuestion, string ScoreQuestion);
public sealed record DecisionResults(ChoiceDecision Choice, AssessmentDecision Noul, ScoreDecision Score);
public sealed record DecisionCapture(
    int FormatVersion, string Provider, string ModelDeployment, DateTimeOffset CapturedAt,
    string ContextHash, DecisionContext Context, JsonElement Results);

public static class DecisionDemo
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static DecisionContext Context()
    {
        var session = new ScheduleCorpus().Sessions.Single(s => s.Id == 13);
        return new(
            $"Attendee interest: practical AI agents in C# and Microsoft Foundry.\n" +
            $"Session: {session.Title}\nAbstract: {session.Abstract}",
            new Dictionary<string, string?>
            {
                ["strong-fit"] = "Directly relevant to practical C# agent development and Foundry.",
                ["partial-fit"] = "Some relevant ideas but not primarily this interest.",
                ["not-a-fit"] = "Not relevant to the stated interest."
            },
            ["Not relevant", "Slightly relevant", "Relevant", "Very relevant"],
            "Which relevance label best fits the attendee's stated interest?",
            "Is this session relevant to practical AI agents in C# and Microsoft Foundry?",
            "How relevant is this abstract to the stated attendee interest on this ordered scale?");
    }

    public static async Task<DecisionResults> EvaluateAsync(
        IDecisionClient client, DecisionContext context, CancellationToken cancellationToken = default)
    {
        var choice = await client.ChooseAsync(context.State, context.ChoiceQuestion, context.Choices, cancellationToken);
        // Noul is the binary task; this library exposes it as AssessAsync / AssessmentDecision.
        var noul = await client.AssessAsync(context.State, context.NoulQuestion, cancellationToken);
        var score = await client.ScoreAsync(context.State, context.ScoreQuestion, context.Scale, cancellationToken);
        var result = new DecisionResults(choice, noul, score);
        ValidateResults(JsonSerializer.SerializeToElement(result), context);
        return result;
    }

    public static DecisionCapture Capture(DecisionResults results, DecisionContext context, string model) =>
        new(1, "Azure OpenAI Decisions", model, DateTimeOffset.UtcNow, Hash(context), context,
            JsonSerializer.SerializeToElement(results));

    public static DecisionCapture ReadCapture(string path, DecisionContext context)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "No verified OpenAI capture exists. First run decisions-record-openai <path> with a configured openaiDecision deployment.", path);
        }
        var capture = JsonSerializer.Deserialize<DecisionCapture>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("The capture file is empty.");
        ValidateCapture(capture, context);
        return capture;
    }

    public static void ValidateCapture(DecisionCapture capture, DecisionContext context)
    {
        if (capture.FormatVersion != 1 || capture.Provider != "Azure OpenAI Decisions" ||
            string.IsNullOrWhiteSpace(capture.ModelDeployment) ||
            capture.CapturedAt == default || capture.CapturedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
            capture.ContextHash != Hash(context) || Hash(capture.Context) != Hash(context))
        {
            throw new InvalidOperationException("The capture provenance or input/task contract does not match this demo.");
        }
        ValidateResults(capture.Results, context);
    }

    public static void ValidateResults(JsonElement results, DecisionContext context)
    {
        var choice = results.GetProperty("Choice");
        var probabilities = choice.GetProperty("Probabilities").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());
        var selected = choice.GetProperty("Choice").GetString();
        var levels = results.GetProperty("Score").GetProperty("LevelProbabilities").EnumerateArray().Select(p => p.GetDouble()).ToArray();
        var score = results.GetProperty("Score").GetProperty("Score").GetDouble();
        var noul = results.GetProperty("Noul").GetProperty("Probability").GetDouble();
        if (selected is null || !context.Choices.ContainsKey(selected) ||
            !probabilities.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(context.Choices.Keys) ||
            !Distribution(probabilities.Values) || levels.Length != context.Scale.Count || !Distribution(levels) ||
            !double.IsFinite(noul) || noul is < 0 or > 1 || !double.IsFinite(score) ||
            Math.Abs(score - levels.Select((p, index) => p * index).Sum()) > 0.001)
        {
            throw new InvalidOperationException("The provider response does not match the choice, Noul, and ordered-score contracts.");
        }
    }

    private static bool Distribution(IEnumerable<double> values) =>
        values.All(v => double.IsFinite(v) && v is >= 0 and <= 1) && Math.Abs(values.Sum() - 1) <= 0.001;

    private static string Hash(DecisionContext context) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(context))));
}
