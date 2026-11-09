using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ConferenceDemos;

public static class DecisionPresentation
{
    public static void Begin(Action<string>? output, string title, string question,
        IEnumerable<string?>? options = null)
    {
        if (output is null) return;
        var text = new StringBuilder($"\n{new string('=', 80)}\n{title}\n{new string('-', 80)}\n");
        AppendWrapped(text, $"Question: {question}");
        if (options is not null)
        {
            foreach (var (option, index) in options.Select((value, index) => (value, index)))
                AppendWrapped(text, $"  {index}: {option}");
        }
        text.AppendLine("State: complete two-day schedule + attendee goal (unchanged).");
        output(text.ToString());
    }

    public static void Provider(Action<string>? output, string provider, string method) =>
        output?.Invoke($"\n{provider} - LIVE\nAPI call: {method} - RUNNING...");

    public static void Result<T>(Action<string>? output, T result) =>
        output?.Invoke(FormatResult(JsonSerializer.SerializeToElement(result)));

    public static string FormatResult(JsonElement result)
    {
        var text = new StringBuilder("API call: COMPLETED\nResult:\n");
        if (result.TryGetProperty("Probability", out var probability))
            text.AppendLine($"  P(yes): {Percent(probability.GetDouble())}");
        else if (result.TryGetProperty("Choice", out var choice))
        {
            var labels = DecisionDemo.SessionChoices();
            var selected = choice.GetString()!;
            AppendWrapped(text, $"  Selected: {selected} - {labels[selected]}");
            text.AppendLine("  Choice probabilities:");
            foreach (var option in result.GetProperty("Probabilities").EnumerateObject()
                .OrderByDescending(p => p.Value.GetDouble()))
                text.AppendLine($"    {option.Name,-5} {Percent(option.Value.GetDouble()),8}");
        }
        else
        {
            text.AppendLine($"  Expected level: {result.GetProperty("Score").GetDouble().ToString("F3", CultureInfo.InvariantCulture)} / 3 (not a percentage)");
            var levels = result.GetProperty("LevelProbabilities").EnumerateArray().ToArray();
            for (var i = 0; i < levels.Length; i++)
                AppendWrapped(text, $"  {i}: {Percent(levels[i].GetDouble())} - {DecisionDemo.ScoreRubric[i]}");
        }
        return text.ToString();
    }

    private static string Percent(double value) => value.ToString("P2", CultureInfo.InvariantCulture);

    private static void AppendWrapped(StringBuilder text, string value)
    {
        var line = new StringBuilder();
        foreach (var word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length + 1 > 96)
            {
                text.AppendLine(line.ToString());
                line.Clear().Append("    ");
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        text.AppendLine(line.ToString());
    }
}
