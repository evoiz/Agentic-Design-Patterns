using System.Globalization;
using System.Text.RegularExpressions;

namespace ConferenceDemos;

public sealed record ScheduleSession(
    int Id, string Title, int Day, string When, string Room, string? Speaker,
    string Url, string Abstract)
{
    public string Citation => $"Update_Conference_Prague_2026_Schedule.md#s{Id}";
    public IReadOnlyList<string> Tracks { get; init; } = [];
    public TimeOnly Start => ParseTime(0);
    public TimeOnly End => ParseTime(1);

    public bool HasTrack(params string[] tracks) =>
        Tracks.Any(t => tracks.Contains(t, StringComparer.OrdinalIgnoreCase));

    private TimeOnly ParseTime(int index)
    {
        var match = Regex.Match(When, @"(?<start>\d{2}:\d{2})\s*[–-]\s*(?<end>\d{2}:\d{2})");
        return match.Success
            ? TimeOnly.ParseExact(match.Groups[index == 0 ? "start" : "end"].Value, "HH:mm", CultureInfo.InvariantCulture)
            : throw new InvalidOperationException($"Session #{Id} has no time range.");
    }
}

public sealed class ScheduleCorpus
{
    public IReadOnlyList<ScheduleSession> Sessions { get; }
    public const string Retrieved = "2026-11-05";

    public ScheduleCorpus() => Sessions = Parse(LoadMarkdown());

    public static string LoadMarkdown()
    {
        using var stream = typeof(ScheduleCorpus).Assembly.GetManifestResourceStream("ConferenceDemos.Schedule.md")
            ?? throw new InvalidOperationException("The canonical schedule is missing from the demo package.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<ScheduleSession> Parse(string markdown)
    {
        var sections = Regex.Matches(markdown,
            """<a id="s(?<id>\d+)"></a>\s*#### #[^\n]*\n(?<body>.*?)(?=<a id="s\d+"></a>|\z)""",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var sessions = new List<ScheduleSession>();
        foreach (Match section in sections)
        {
            var body = section.Groups["body"].Value;
            var heading = Regex.Match(section.Value, @"#### #\d+ · (?<title>[^\r\n]+)");
            var when = Field(body, "When");
            var day = Regex.Match(when, @"^Day (?<day>[12]),");
            var abstractStart = body.IndexOf("**Abstract**", StringComparison.Ordinal);
            if (!heading.Success || !day.Success || abstractStart < 0)
            {
                throw new InvalidOperationException("A schedule entry has no title, day, or abstract.");
            }

            var abstractText = body[(abstractStart + "**Abstract**".Length)..].Trim();
            // Day headings between the last session of each day are not session evidence.
            abstractText = Regex.Split(abstractText, @"(?m)^---\s*$|^## Day ")[0].Trim();
            sessions.Add(new ScheduleSession(
                int.Parse(section.Groups["id"].Value, CultureInfo.InvariantCulture),
                heading.Groups["title"].Value.Trim(),
                int.Parse(day.Groups["day"].Value, CultureInfo.InvariantCulture),
                when, Field(body, "Room"), Speaker(body), Field(body, "Link"), abstractText)
            {
                Tracks = Regex.Matches(body[..abstractStart], @"(?m)^- \*\*Track:\*\* (?<value>[^\r\n]+)")
                    .Select(m => m.Groups["value"].Value.Trim()).ToArray()
            });
        }

        if (sessions.Count == 0 || sessions.Select(s => s.Id).Distinct().Count() != sessions.Count)
        {
            throw new InvalidOperationException("The canonical schedule contains no sessions or duplicate identifiers.");
        }

        return sessions;
    }

    private static string Field(string text, string name)
    {
        var match = Regex.Match(text, $@"(?m)^- \*\*{Regex.Escape(name)}:\*\* (?<value>[^\r\n]+)");
        return match.Success ? match.Groups["value"].Value.Trim()
            : throw new InvalidOperationException($"Schedule field '{name}' is missing.");
    }

    private static string? Speaker(string text)
    {
        var match = Regex.Match(text, @"(?m)^- \*\*Speakers?:\*\* (?<value>[^\r\n]+)");
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }
}
