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
        Assert.StartsWith("https://", profile.Photo);
        var skill = File.ReadAllText(Path.Combine(SpeakerPageDemo.SkillsDirectory, "speaker-agenda-page", "SKILL.md"));
        Assert.Contains("name: speaker-agenda-page", skill);
        Assert.Contains("Use web search", SpeakerPageDemo.Prompt(profile, webSearch: true));
        Assert.Contains("Web search is off", SpeakerPageDemo.Prompt(profile, webSearch: false));
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
        var agent = SpeakerPageDemo.CreateAgent(new FakeClient(), dir.Path, webSearch: true);
        Assert.Equal("SpeakerPageBuilder", agent.Name);
    }

    [Fact]
    public void Verify_shows_success_for_a_good_page_and_failures_for_a_bad_one()
    {
        using var dir = new TempDir();
        var profile = SpeakerPageDemo.LoadProfile();
        var path = Path.Combine(dir.Path, SpeakerPageDemo.PageFile);
        Assert.False(SpeakerPageDemo.Verify(path, profile, false).Single().Passed);

        File.WriteAllText(path, Page(profile, profile.Photo!, [1, 6, 13], """<a href="https://learn.microsoft.com/agent-framework">MAF</a>"""));
        Assert.All(SpeakerPageDemo.Verify(path, profile, webSearch: true), c => Assert.True(c.Passed, $"{c.Check}: {c.Evidence}"));

        File.WriteAllText(path, Page(profile, "other.png", [1, 2, 999], ""));
        var checks = SpeakerPageDemo.Verify(path, profile, webSearch: true).ToDictionary(c => c.Check);
        Assert.False(checks["Photo"].Passed);
        Assert.False(checks["My talk"].Passed);
        Assert.False(checks["Real sessions"].Passed);
        Assert.False(checks["No overlaps"].Passed);
        Assert.False(checks["Web links"].Passed);
    }

    private static string Page(SpeakerProfile profile, string photo, int[] ids, string links) => $"""
        <!doctype html><html><body><header><img src="{photo}"><h1>{profile.Name}</h1></header>
        {string.Join("\n", ids.Select(id => $"""<article class="session" data-session-id="{id}"></article>"""))}
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
