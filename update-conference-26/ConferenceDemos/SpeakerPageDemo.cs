using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgenticDesignPatterns.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ConferenceDemos;

public sealed record SpeakerProfile(
    string Name, string Role, int TalkId, string? GitHub, string? Photo, string Repository, string[] Topics);
public sealed record PageCheck(string Check, bool Passed, string Evidence);

/// <summary>
/// A MAF HarnessAgent builds an HTML speaker agenda page. The harness brings the agent loop, todos,
/// skills, sandboxed file tools, and hosted web search; this demo adds only two schedule tools and a skill.
/// </summary>
public static class SpeakerPageDemo
{
    public const string PageFile = "agenda.html";
    public static string SkillsDirectory => Path.Combine(AppContext.BaseDirectory, "Skills");
    public static string DefaultOutputDirectory => Path.GetFullPath(Path.Combine("out", "speaker-page"));

    private static readonly ScheduleCorpus Schedule = new();

#pragma warning disable MAAI001 // HarnessAgent is experimental in MAF 1.22.
    public static HarnessAgent CreateAgent(IChatClient client, string outputDirectory, bool webSearch) =>
        client.AsHarnessAgent(new HarnessAgentOptions
        {
            Name = "SpeakerPageBuilder",
            ChatOptions = new ChatOptions
            {
                Tools = [AIFunctionFactory.Create(ListSessions), AIFunctionFactory.Create(GetSession)],
                AllowMultipleToolCalls = false // one tool call per turn keeps the persisted tool history valid
            },
            AgentSkillsSource = new AgentFileSkillsSource(SkillsDirectory),  // how to build the page
            ToolApprovalAgentOptions = new ToolApprovalAgentOptions
            {
                AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule] // reading a skill is safe
            },
            FileAccessStore = new FileSystemAgentFileStore(outputDirectory), // the only folder it can write to
            FileAccessProviderOptions = new FileAccessProviderOptions
            {
                DisableReadOnlyToolApproval = true, DisableWriteToolApproval = true // the sandbox is the boundary
            },
            DisableWebSearch = !webSearch,  // hosted web_search tool, executed by Foundry
            DisableAgentModeProvider = true // just execute; no plan-then-approve stop
        });
#pragma warning restore MAAI001

    public static string Prompt(SpeakerProfile profile, bool webSearch) => $"""
        Build my personal speaker agenda page for Update Conference Prague 2026 and save it as {PageFile}.
        Use the speaker-agenda-page skill. Pick 4-5 sessions on the day of my talk that fit my topics.
        {(webSearch ? "Use web search to add 2-3 current links about my topics." : "Web search is off: add no further-reading links.")}
        Speaker profile: {JsonSerializer.Serialize(profile)}
        """;

    public static async Task<IReadOnlyList<PageCheck>> RunAsync(
        IChatClient client, SpeakerProfile profile, string outputDirectory, bool webSearch,
        Action<string> log, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var pagePath = Path.Combine(outputDirectory, PageFile);
        File.Delete(pagePath);

        var agent = CreateAgent(client, outputDirectory, webSearch);
        // Streaming only so the audience sees every harness tool call live.
        await foreach (var update in agent.RunStreamingAsync(Prompt(profile, webSearch), cancellationToken: cancellationToken))
        {
            foreach (var content in update.Contents)
            {
                if (content is FunctionCallContent call)
                {
                    var args = string.Join(", ", call.Arguments?.Values.Select(v => $"{v}") ?? []).ReplaceLineEndings(" ");
                    log($"  🔧 {call.Name}({(args.Length > 60 ? args[..60] + "…" : args)})");
                }
                else if (content is ToolApprovalRequestContent approval)
                {
                    log($"  ⏸ approval needed: {approval.ToolCall}");
                }
                else if (content.GetType().Name.Contains("WebSearch", StringComparison.Ordinal))
                {
                    log("  🌐 web_search");
                }
            }
        }
        return Verify(pagePath, profile, webSearch);
    }

    /// <summary>A few code-owned checks so success is visible and not just the model's claim.</summary>
    public static IReadOnlyList<PageCheck> Verify(string pagePath, SpeakerProfile profile, bool webSearch)
    {
        if (!File.Exists(pagePath))
        {
            return [new("Page saved", false, $"{PageFile} was not written")];
        }
        var html = File.ReadAllText(pagePath);
        var ids = Regex.Matches(html, @"data-session-id=[""'](\d+)[""']").Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToArray();
        var unknown = ids.Where(id => Schedule.Sessions.All(s => s.Id != id)).ToArray();
        var picked = Schedule.Sessions.Where(s => ids.Contains(s.Id)).ToArray();
        var clashes = picked.SelectMany(a => picked.Where(b => a.Id < b.Id && a.Day == b.Day && a.Start < b.End && b.Start < a.End)
            .Select(b => $"#{a.Id}/#{b.Id}")).ToArray();
        var links = Regex.Matches(html, @"href=[""'](https://[^""']+)[""']").Select(m => m.Groups[1].Value)
            .Where(u => !u.Contains("updateconference", StringComparison.OrdinalIgnoreCase) && !u.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            .Distinct().ToArray();
        List<PageCheck> checks =
        [
            new("Page saved", html.Contains("</html>", StringComparison.OrdinalIgnoreCase), $"{new FileInfo(pagePath).Length / 1024.0:0.0} KB"),
            new("Speaker", html.Contains(profile.Name, StringComparison.Ordinal), profile.Name),
            new("Photo", profile.Photo is null || html.Contains(profile.Photo, StringComparison.Ordinal), profile.Photo ?? "initials avatar"),
            new("My talk", ids.Contains(profile.TalkId), $"#{profile.TalkId} on the timeline"),
            new("Real sessions", ids.Length >= 3 && unknown.Length == 0,
                unknown.Length > 0 ? "unknown " + string.Join(" ", unknown.Select(i => "#" + i)) : string.Join(" ", ids.Select(i => "#" + i))),
            new("No overlaps", clashes.Length == 0, clashes.Length == 0 ? "one session at a time" : "clash " + string.Join(" ", clashes))
        ];
        if (webSearch)
        {
            checks.Add(new("Web links", links.Length > 0, links.Length == 0 ? "none found" : string.Join(", ", links.Take(3))));
        }
        return checks;
    }

    [Description("List the sessions of a conference day (1 or 2): id, time, room, title, tracks, speaker.")]
    public static string ListSessions([Description("Conference day: 1 or 2.")] int day) =>
        JsonSerializer.Serialize(Schedule.Sessions.Where(s => s.Day == day).OrderBy(s => s.Start).Select(s => new
        {
            s.Id, Start = s.Start.ToString("HH:mm"), End = s.End.ToString("HH:mm"), s.Room, s.Title, s.Tracks, s.Speaker
        }));

    [Description("Get one session with its day, abstract, and official URL.")]
    public static string GetSession([Description("Session id, for example 13.")] int id) =>
        Schedule.Sessions.SingleOrDefault(s => s.Id == id) is { } s
            ? JsonSerializer.Serialize(new { s.Id, s.Day, s.When, s.Room, s.Title, s.Speaker, s.Tracks, s.Url, s.Abstract })
            : $"Session #{id} does not exist.";

    public static SpeakerProfile LoadProfile(string? path = null) =>
        JsonSerializer.Deserialize<SpeakerProfile>(
            File.ReadAllText(path ?? Path.Combine(AppContext.BaseDirectory, "Samples", "speaker-profile.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("The speaker profile is empty.");

    /// <summary>Responses API client: needed for the hosted web_search tool, and works for function tools too.</summary>
    public static IChatClient CreateClient(FoundrySettings settings)
    {
#pragma warning disable OPENAI001
        return new OpenAIClient(new System.ClientModel.ApiKeyCredential(settings.ApiKey),
                new OpenAIClientOptions { Endpoint = settings.OpenAIEndpoint })
            .GetResponsesClient().AsIChatClient(settings.ResolveModelDeployment(null));
#pragma warning restore OPENAI001
    }

    public static void Open(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
}
