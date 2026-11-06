using ConferenceDemos;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class SpeakerPageTests
{
    private static readonly int[] Agenda = [1, 6, 13, 17, 22];

    [Fact]
    public void Profile_and_skill_resolve_from_the_build_output()
    {
        var profile = SpeakerPageDemo.LoadProfile();
        Assert.Equal(13, profile.TalkId);
        Assert.False(string.IsNullOrWhiteSpace(profile.Name));
        var skill = File.ReadAllText(Path.Combine(SpeakerPageDemo.SkillsDirectory, "speaker-agenda-page", "SKILL.md"));
        Assert.Contains("name: speaker-agenda-page", skill);
        Assert.Contains("data-session-id", skill);
    }

    [Fact]
    public void Prompt_switches_further_reading_with_web_search()
    {
        var profile = SpeakerPageDemo.LoadProfile();
        Assert.Contains("Web search is enabled", SpeakerPageDemo.Prompt(profile, AgendaPlanningDemo.DefaultRequest, true));
        Assert.Contains("do not invent links", SpeakerPageDemo.Prompt(profile, AgendaPlanningDemo.DefaultRequest, false));
    }

    [Fact]
    public void Validator_accepts_a_grounded_self_contained_page()
    {
        using var dir = new TempDir();
        File.WriteAllBytes(Path.Combine(dir.Path, "speaker.png"), [1, 2, 3]);
        var schedule = new ScheduleCorpus();
        var profile = SpeakerPageDemo.LoadProfile();
        var html = Page(schedule, profile, Agenda, """<img src="speaker.png" alt="photo">""",
            """<a href="https://learn.microsoft.com/agent-framework">MAF docs</a>""");

        var checks = SpeakerPageDemo.ValidatePage(html, html.Length, profile, AgendaPlanningDemo.DefaultRequest, schedule,
            dir.Path, "speaker.png", webSearch: true);

        Assert.All(checks, c => Assert.True(c.Passed, $"{c.Check}: {c.Evidence}"));
        Assert.Contains(checks, c => c.Check == "Web links");
    }

    [Fact]
    public void Validator_rejects_scripts_broken_images_unmet_goals_and_invented_titles()
    {
        using var dir = new TempDir();
        var schedule = new ScheduleCorpus();
        var profile = SpeakerPageDemo.LoadProfile();
        var html = Page(schedule, profile, [1, 2, 10], """<img src="https://example.com/me.png"><script>alert(1)</script>""", "")
            .Replace(schedule.Sessions.Single(s => s.Id == 1).Title, "A made-up keynote", StringComparison.Ordinal);

        var checks = SpeakerPageDemo.ValidatePage(html, html.Length, profile, AgendaPlanningDemo.DefaultRequest, schedule,
            dir.Path, null, webSearch: true).ToDictionary(c => c.Check);

        Assert.False(checks["Self-contained"].Passed);
        Assert.False(checks["Photo"].Passed);
        Assert.False(checks["Agenda goals"].Passed);
        Assert.False(checks["Grounded titles"].Passed);
        Assert.False(checks["Web links"].Passed);
        Assert.False(SpeakerPageDemo.ValidatePage(null, 0, profile, AgendaPlanningDemo.DefaultRequest, schedule, dir.Path, null, false)
            .Single().Passed);
    }

    [Fact]
    public async Task Photo_preparation_copies_local_images_and_falls_back_for_unsupported_files()
    {
        using var dir = new TempDir();
        var source = Path.Combine(dir.Path, "me.jpg");
        File.WriteAllBytes(source, [0xFF, 0xD8, 0xFF]);
        var output = Path.Combine(dir.Path, "out");

        Assert.Equal("speaker.jpg", await SpeakerPageDemo.PreparePhotoAsync(source, output, null, CancellationToken.None));
        Assert.True(File.Exists(Path.Combine(output, "speaker.jpg")));
        Assert.Null(await SpeakerPageDemo.PreparePhotoAsync(Path.Combine(dir.Path, "missing.png"), output, null, CancellationToken.None));
        Assert.Null(await SpeakerPageDemo.PreparePhotoAsync(null, output, null, CancellationToken.None));
    }

    private static string Page(ScheduleCorpus schedule, SpeakerProfile profile, int[] ids, string extra, string links)
    {
        var talk = schedule.Sessions.Single(s => s.Id == profile.TalkId);
        var items = string.Join("\n", ids.Select(id => schedule.Sessions.Single(s => s.Id == id))
            .Select(s => $"""<li data-session-id="{s.Id}">{s.Start:HH\:mm} {System.Net.WebUtility.HtmlEncode(s.Title)}</li>"""));
        return $"""
            <!doctype html><html><head><style>body{"{"}font-family:sans-serif{"}"}</style></head><body>
            <header>{extra}<h1>{profile.Name}</h1><p>{System.Net.WebUtility.HtmlEncode(talk.Title)}</p></header>
            <ol>{items}</ol>{links}</body></html>
            """;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("speaker-page-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
