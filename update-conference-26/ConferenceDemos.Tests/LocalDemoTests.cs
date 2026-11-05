using System.Text.Json;
using AgenticDesignPatterns.TestSupport;
using ConferenceDemos;
using Microsoft.Extensions.AI;
using ScheduleAgent.Hosted;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class LocalDemoTests
{
    [Fact]
    public void Embedded_schedule_contains_all_48_sessions_and_preserves_fields()
    {
        var corpus = new ScheduleCorpus();
        Assert.Equal(48, corpus.Sessions.Count);
        Assert.Equal(25, corpus.Sessions.Count(s => s.Day == 1));
        Assert.Equal(23, corpus.Sessions.Count(s => s.Day == 2));
        Assert.Null(corpus.Sessions.Single(s => s.Id == 25).Speaker);
        Assert.Contains("Stefan", corpus.Sessions.Single(s => s.Id == 47).Speaker);
        Assert.DoesNotContain("At a glance", corpus.Sessions.Single(s => s.Id == 25).Abstract);
        Assert.All(corpus.Sessions, s =>
        {
            Assert.StartsWith("https://prague.updateconference.net/", s.Url);
            Assert.Contains("CET", s.When);
            Assert.StartsWith("Hall ", s.Room);
            Assert.EndsWith($"#s{s.Id}", s.Citation);
        });
    }

    [Fact]
    public void Retrieval_finds_agent_sessions_and_respects_day_and_word_boundaries()
    {
        var corpus = new ScheduleCorpus();
        Assert.Contains(corpus.Search("Foundry AI .NET", 1), s => s.Id == 13);
        Assert.All(corpus.Search(ScheduleDemo.DefaultQuestion, 1), s => Assert.Equal(1, s.Day));
        Assert.Contains(corpus.Search("inference engine", 2), s => s.Id == 42);
        Assert.DoesNotContain(corpus.Search("AI", 1), s => s.Id == 24);
        Assert.Empty(corpus.Search("unicorn astrophysics", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => corpus.Search("AI", 3));
        Assert.Throws<InvalidOperationException>(() => ScheduleCorpus.Parse("not a schedule"));
    }

    [Fact]
    public async Task Schedule_agent_reads_tool_and_prints_only_validated_final_recommendations()
    {
        var calls = 0;
        using var client = ScheduleClient(() => calls++);
        var answer = await ScheduleDemo.RunAsync(client, ScheduleDemo.DefaultQuestion);
        Assert.Equal(2, calls);
        Assert.Contains("Agentic AI on Microsoft Foundry", answer);
        Assert.Contains("13:45", answer);
        Assert.Contains("Update_Conference_Prague_2026_Schedule.md#s13", answer);
        Assert.DoesNotContain("\"SearchText\"", answer);
        Assert.DoesNotContain("RetrieveSchedule", answer);
    }

    [Fact]
    public async Task No_match_does_not_call_the_model_and_cancelled_requests_fail()
    {
        using var client = new ScriptedChatClient((_, _) => throw new InvalidOperationException("No model call expected."));
        Assert.Contains("No matching session", await ScheduleDemo.RunAsync(client, "unicorn astrophysics"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ScheduleDemo.RunAsync(client, "unicorn", cancellationToken: new CancellationToken(true)));
        await Assert.ThrowsAsync<ArgumentException>(() => ScheduleDemo.RunAsync(client, new string('x', 4001)));
    }

    [Fact]
    public void Fabricated_session_quote_duplicate_or_wrong_day_is_rejected()
    {
        var evidence = new ScheduleCorpus().Search("Foundry", 1);
        Assert.Throws<InvalidOperationException>(() => ScheduleDemo.Format(new([new(999, "invented")]), evidence, 1));
        Assert.Throws<InvalidOperationException>(() => ScheduleDemo.Format(new([new(13, "invented")]), evidence, 1));
        var recommendation = new SessionRecommendation(13, evidence.Single(s => s.Id == 13).Abstract[..50]);
        Assert.Throws<InvalidOperationException>(() => ScheduleDemo.Format(new([recommendation, recommendation]), evidence, 1));
        Assert.Throws<InvalidOperationException>(() => ScheduleDemo.Format(new([recommendation]), evidence, 2));
    }

    [Fact]
    public async Task Skipping_schedule_retrieval_cannot_return_success()
    {
        using var client = new ScriptedChatClient((_, _) => Json(new ScheduleAnswer([new(13, "not evidence")])));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ScheduleDemo.RunAsync(client, "Foundry"));
    }

    [Fact]
    public async Task Shopping_workflow_returns_missing_items_and_goal_status()
    {
        using var client = new ScriptedChatClient((_, _) => Json(new MealChoice("chickpea-salad")));
        var result = await ShoppingDemo.RunAsync(client, ShoppingDemo.Sample("vegan"));
        Assert.True(result.Passed);
        Assert.Equal(["tomato", "cucumber"], result.Missing);
        Assert.DoesNotContain("olive oil", result.Missing);
        Assert.Contains("PASS", result.Status);
    }

    [Fact]
    public async Task Shopping_no_match_and_incomplete_pantry_do_not_return_a_completed_list()
    {
        var calls = 0;
        using var client = new ScriptedChatClient((_, _) =>
        {
            calls++;
            return Json(new MealChoice("chickpea-salad"));
        });
        var unmatched = await ShoppingDemo.RunAsync(client, ShoppingDemo.Sample("keto"));
        Assert.False(unmatched.Passed);
        Assert.Equal(0, calls);
        var sample = ShoppingDemo.Sample("vegan");
        var incomplete = sample with { Pantry = sample.Pantry.Where(p => p.Key != "tomato").ToDictionary() };
        var result = await ShoppingDemo.RunAsync(client, incomplete);
        Assert.False(result.Passed);
        Assert.Empty(result.Missing);
        Assert.Contains("incomplete", result.Status);
        var unknown = sample with { Pantry = sample.Pantry.ToDictionary(p => p.Key, p => p.Key == "tomato" ? null : p.Value) };
        Assert.False((await ShoppingDemo.RunAsync(client, unknown)).Passed);
    }

    [Fact]
    public void Shopping_goal_gate_rejects_extra_missing_duplicate_and_wrong_preference_items()
    {
        var request = ShoppingDemo.Sample("vegan");
        var recipe = ShoppingDemo.Recipes[0];
        Assert.False(ShoppingDemo.Validate(request, recipe, ["tomato"]).Passed);
        Assert.False(ShoppingDemo.Validate(request, recipe, ["tomato", "cucumber", "olive oil"]).Passed);
        Assert.False(ShoppingDemo.Validate(request, recipe, ["tomato", "cucumber", "tomato"]).Passed);
        Assert.False(ShoppingDemo.Validate(request, ShoppingDemo.Recipes[1], ["cheese", "tomato"]).Passed);
    }

    [Fact]
    public async Task Shopping_model_failure_propagates()
    {
        using var client = new ScriptedChatClient((_, _) => throw new HttpRequestException("Inference failed."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ShoppingDemo.RunAsync(client, ShoppingDemo.Sample()));
    }

    [Fact]
    public async Task Hosted_responses_use_same_fixture_and_return_actual_final_answer()
    {
        using var client = ScheduleClient();
        await using var host = await HostedResponses.StartAsync(new ScheduleHostedAgent(client));
        using var result = await host.InvokeAsync("Foundry", "update-schedule");
        Assert.Equal("completed", result.RootElement.GetProperty("status").GetString());
        var answer = HostedResponses.OutputText(result.RootElement);
        Assert.Contains("#s13", answer);
        Assert.DoesNotContain("\"SearchText\"", answer);
    }

    [Fact]
    public async Task Hosted_requests_are_isolated_and_stream_completed_results()
    {
        using var client = ScheduleClient();
        var agent = new ScheduleHostedAgent(client);
        var session = await agent.CreateSessionAsync();
        Assert.Contains("#s13", (await agent.RunAsync("Foundry", session)).Text);
        var updates = new List<string>();
        await foreach (var update in agent.RunStreamingAsync("unicorn astrophysics", session))
        {
            updates.Add(update.Text);
        }
        Assert.Contains("No matching session", Assert.Single(updates));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RunAsync(new string('x', 4001)));
    }

    internal static ChatResponse Json<T>(T value) => new(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(value)));

    internal static ScriptedChatClient ScheduleClient(Action? onCall = null) => new((messages, options) =>
    {
        onCall?.Invoke();
        if (!messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any())
        {
            var tool = Assert.Single(options?.Tools ?? []);
            Assert.Equal("RetrieveSchedule", tool.Name);
            return new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("schedule-1", tool.Name, new Dictionary<string, object?>())]));
        }
        var session = new ScheduleCorpus().Sessions.Single(s => s.Id == 13);
        return Json(new ScheduleAnswer([new(13, session.Abstract[..80])]));
    });
}
