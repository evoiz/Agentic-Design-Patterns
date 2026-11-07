using System.Text.Json;
using ConferenceDemos;
using Microsoft.Extensions.AI;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class SpeakerPageTests
{
    [Fact]
    public void Profile_skill_and_prompt_are_ready()
    {
        var profile = SpeakerPageDemo.LoadProfile();
        Assert.Equal(13, profile.TalkId);
        Assert.Equal("cedricmendelin", profile.GitHub);
        var skill = File.ReadAllText(Path.Combine(SpeakerPageDemo.SkillsDirectory, "speaker-agenda-page", "SKILL.md"));
        Assert.Contains("name: speaker-agenda-page", skill);
        Assert.Contains("find my GitHub username", SpeakerPageDemo.Prompt(profile, webSearch: true));
        Assert.DoesNotContain("cedricmendelin\"", SpeakerPageDemo.Prompt(profile, webSearch: true));
        Assert.Contains("my GitHub username is cedricmendelin", SpeakerPageDemo.Prompt(profile, webSearch: false));
    }

    [Fact]
    public void Schedule_tools_return_real_sessions()
    {
        var day = JsonDocument.Parse(SpeakerPageDemo.GetSession(13)).RootElement.GetProperty("Day").GetInt32();
        Assert.Contains(JsonDocument.Parse(SpeakerPageDemo.ListSessions(day)).RootElement.EnumerateArray(),
            s => s.GetProperty("Id").GetInt32() == 13);
        Assert.Contains("does not exist", SpeakerPageDemo.GetSession(999));
    }

    [Fact]
    public void Harness_agent_is_built_from_options()
    {
        using var dir = new TempDir();
        var agent = SpeakerPageDemo.CreateAgent(new FakeClient(), SpeakerPageDemo.LoadProfile(), dir.Path, webSearch: true);
        Assert.Equal("SpeakerPageBuilder", agent.Name);
    }

    [Fact]
    public void Verify_shows_success_for_a_good_page_and_failures_for_a_bad_one()
    {
        using var dir = new TempDir();
        var profile = SpeakerPageDemo.LoadProfile();
        var path = Path.Combine(dir.Path, SpeakerPageDemo.PageFile);
        Assert.False(SpeakerPageDemo.Verify(path, profile, false).Single().Passed);

        File.WriteAllBytes(Path.Combine(dir.Path, "speaker.png"), [1, 2, 3]);
        File.WriteAllText(path, Page(profile, "speaker.png", [1, 6, 13], """<a href="https://learn.microsoft.com/agent-framework">MAF</a>"""));
        Assert.All(SpeakerPageDemo.Verify(path, profile, webSearch: true), c => Assert.True(c.Passed, $"{c.Check}: {c.Evidence}"));
        Assert.Equal("All checks passed.", SpeakerPageDemo.CheckPage(path, profile, webSearch: true));

        File.WriteAllText(path, Page(profile, "other.png", [1, 2, 999], ""));
        var checks = SpeakerPageDemo.Verify(path, profile, webSearch: true).ToDictionary(c => c.Check);
        Assert.False(checks["Photo"].Passed);
        Assert.False(checks["My talk"].Passed);
        Assert.False(checks["Real sessions"].Passed);
        Assert.False(checks["No overlaps"].Passed);
        Assert.False(checks["Web links"].Passed);
        Assert.Contains("overwrite: true", SpeakerPageDemo.CheckPage(path, profile, webSearch: true));
    }

    [Fact]
    public void Verify_requires_own_talk_in_timeline_not_only_in_a_separate_card()
    {
        using var dir = new TempDir();
        var profile = SpeakerPageDemo.LoadProfile();
        var path = Path.Combine(dir.Path, SpeakerPageDemo.PageFile);
        File.WriteAllText(path, Page(profile, "speaker.png", [1, 6, 17, 22], "") +
            $"""<article data-session-id="{profile.TalkId}">My talk</article>""");
        var checks = SpeakerPageDemo.Verify(path, profile, false).ToDictionary(c => c.Check);
        Assert.False(checks["My talk"].Passed);
        Assert.True(checks["Real sessions"].Passed);
        Assert.True(checks["No overlaps"].Passed);
    }

    [Theory]
    [InlineData("https://github.com/microsoft/agent-framework", true)]
    [InlineData("https://github.com/cedricmendelin", false)]
    [InlineData("https://prague.updateconference.net/sessions/example", false)]
    public void Verify_accepts_github_reading_but_not_the_speaker_profile_or_conference_link(string link, bool passed)
    {
        using var dir = new TempDir();
        var profile = SpeakerPageDemo.LoadProfile();
        var path = Path.Combine(dir.Path, SpeakerPageDemo.PageFile);
        File.WriteAllText(path, Page(profile, "speaker.png", [1, 6, 13], $"""<a href="{link}">Reading</a>"""));
        Assert.Equal(passed, SpeakerPageDemo.Verify(path, profile, true).Single(c => c.Check == "Web links").Passed);
    }

    [Fact]
    public void Verify_does_not_count_the_profile_repository_as_further_reading()
    {
        using var dir = new TempDir();
        var profile = SpeakerPageDemo.LoadProfile();
        var path = Path.Combine(dir.Path, SpeakerPageDemo.PageFile);
        File.WriteAllText(path, Page(profile, "speaker.png", [1, 6, 13], $"""<a href="{profile.Repository}">Repository</a>"""));
        Assert.False(SpeakerPageDemo.Verify(path, profile, true).Single(c => c.Check == "Web links").Passed);
    }

    [Fact]
    public async Task Avatar_download_accepts_only_plain_usernames()
    {
        using var dir = new TempDir();
        foreach (var bad in new[] { "https://evil.example/x.png", "../secret", "a b", "" })
        {
            Assert.Contains("plain GitHub username", await SpeakerPageDemo.DownloadGitHubAvatarAsync(bad, dir.Path, CancellationToken.None));
        }
        Assert.Empty(Directory.EnumerateFiles(dir.Path));
    }

    private static string Page(SpeakerProfile profile, string photo, int[] ids, string links) => $"""
        <!doctype html><html><body><header><img src="{photo}"><h1>{profile.Name}</h1></header>
        <section id="timeline">{string.Join("\n", ids.Select(id => $"""<article class="session" data-session-id="{id}"></article>"""))}</section>
        {links}</body></html>
        """;

    private sealed class FakeClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("speaker-page-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
