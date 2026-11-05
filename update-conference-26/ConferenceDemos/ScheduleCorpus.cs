using System.Globalization;
using System.Text.RegularExpressions;

namespace ConferenceDemos;

public sealed record ScheduleSession(
    int Id, string Title, int Day, string When, string Room, string? Speaker,
    string Url, string Abstract, string SearchText)
{
    public string Citation => $"Update_Conference_Prague_2026_Schedule.md#s{Id}";
}

public sealed class ScheduleCorpus
{
    public IReadOnlyList<ScheduleSession> Sessions { get; }
    public const string Retrieved = "2026-11-05";

    public ScheduleCorpus()
    {
        using var stream = typeof(ScheduleCorpus).Assembly.GetManifestResourceStream("ConferenceDemos.Schedule.md")
            ?? throw new InvalidOperationException("The canonical schedule is missing from the demo package.");
        using var reader = new StreamReader(stream);
        Sessions = Parse(reader.ReadToEnd());
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
                when, Field(body, "Room"), Speaker(body), Field(body, "Link"),
                abstractText, heading.Value + "\n" + body[..(abstractStart + "**Abstract**".Length)] + "\n" + abstractText));
        }

        if (sessions.Count == 0 || sessions.Select(s => s.Id).Distinct().Count() != sessions.Count)
        {
            throw new InvalidOperationException("The canonical schedule contains no sessions or duplicate identifiers.");
        }

        return sessions;
    }

    public IReadOnlyList<ScheduleSession> Search(string interest, int day, int limit = 8)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interest);
        if (day is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(day), "Use conference day 1 or 2, not the machine's current date.");
        }
        if (limit is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "what", "should", "i", "attend", "today", "if", "interested", "in", "and",
            "the", "a", "an", "is", "about", "me", "my", "for", "please", "sessions", "session"
        };
        var terms = Regex.Matches(interest, @"\.NET\b|[\p{L}\p{N}]+", RegexOptions.IgnoreCase)
            .Select(m => m.Value).Where(t => !stopWords.Contains(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return Sessions.Where(s => s.Day == day)
            .Select(s => (Session: s, Score: terms.Count(t => Regex.IsMatch(
                s.SearchText, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(t)}(?![\p{{L}}\p{{N}}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))))
            .Where(s => s.Score > 0).OrderByDescending(s => s.Score).ThenBy(s => s.Session.Id)
            .Take(limit).Select(s => s.Session).ToArray();
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
