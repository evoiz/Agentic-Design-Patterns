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
    public void Embedded_markdown_preserves_both_days_and_all_session_anchors()
    {
        var markdown = ScheduleCorpus.LoadMarkdown();
        Assert.Contains("## Day 1", markdown);
        Assert.Contains("## Day 2", markdown);
        Assert.All(new ScheduleCorpus().Sessions, session =>
            Assert.Contains($"<a id=\"s{session.Id}\"></a>", markdown));
        Assert.Throws<InvalidOperationException>(() => ScheduleCorpus.Parse("not a schedule"));
    }

    [Fact]
    public async Task Schedule_provider_supplies_both_days_and_preserves_question_before_one_model_call()
    {
        var calls = 0;
        const string question = "What should I attend on day 2 if I'm interested in AI and .NET?";
        const string expected = "A normal grounded answer without a JSON contract.";
        using var client = new ScriptedChatClient((messages, options) =>
        {
            calls++;
            var request = messages.ToArray();
            AssertScheduleContext(request, options);
            Assert.Contains(request, message => message.Role == ChatRole.User &&
                message.Text.Contains(question, StringComparison.Ordinal));
            return new(new ChatMessage(ChatRole.Assistant, expected));
        });

        var answer = await ScheduleDemo.RunAsync(client, question);

        Assert.Equal(1, calls);
        Assert.Equal(expected, answer);
    }

    [Fact]
    public async Task Schedule_agent_returns_only_the_grounded_final_text()
    {
        using var client = ScheduleClient();
        var answer = await ScheduleDemo.RunAsync(client, ScheduleDemo.DefaultQuestion);
        Assert.Contains("Agentic AI on Microsoft Foundry", answer);
        Assert.Contains("13:45", answer);
        Assert.Contains("Update_Conference_Prague_2026_Schedule.md#s13", answer);
        Assert.DoesNotContain("## Day 1", answer);
    }

    [Fact]
    public async Task Unrelated_interest_reaches_the_model_with_context_and_returns_its_limitation()
    {
        var calls = 0;
        using var client = ScheduleClient(() => calls++);
        var answer = await ScheduleDemo.RunAsync(client, "unicorn astrophysics");
        Assert.Equal(1, calls);
        Assert.Contains("No matching session", answer);
    }

    [Fact]
    public async Task Invalid_inputs_and_cancelled_requests_fail_before_calling_the_model()
    {
        using var client = new ScriptedChatClient((_, _) => throw new InvalidOperationException("No model call expected."));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ScheduleDemo.RunAsync(client, "unicorn", cancellationToken: new CancellationToken(true)));
        await Assert.ThrowsAsync<ArgumentException>(() => ScheduleDemo.RunAsync(client, new string('x', 4001)));
        await Assert.ThrowsAsync<ArgumentException>(() => ScheduleDemo.RunAsync(client, " "));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Empty_schedule_responses_fail_explicitly(string text)
    {
        using var client = new ScriptedChatClient((_, _) => new(new ChatMessage(ChatRole.Assistant, text)));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ScheduleDemo.RunAsync(client, "Foundry"));
        Assert.Contains("empty response", error.Message);
    }

    [Fact]
    public async Task Schedule_inference_failure_propagates()
    {
        using var client = new ScriptedChatClient((_, _) => throw new HttpRequestException("Inference failed."));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => ScheduleDemo.RunAsync(client, "Foundry"));
        Assert.Equal("Inference failed.", error.Message);
    }

    [Fact]
    public async Task Cancellation_during_inference_cannot_return_an_answer()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new ScriptedChatClient((_, _) =>
        {
            cancellation.Cancel();
            return new(new ChatMessage(ChatRole.Assistant, "Do not return this cancelled answer."));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ScheduleDemo.RunAsync(client, "Foundry", cancellationToken: cancellation.Token));
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
        Assert.DoesNotContain("## Day 1", answer);
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
        var request = messages.ToArray();
        AssertScheduleContext(request, options);
        var question = Assert.Single(request, message => message.Role == ChatRole.User &&
            !message.Text.Contains(ScheduleCorpus.LoadMarkdown(), StringComparison.Ordinal)).Text;
        if (question.Contains("unicorn astrophysics", StringComparison.Ordinal))
        {
            Assert.DoesNotContain(ScheduleDemo.DefaultQuestion, question);
            Assert.DoesNotContain(request, message => message.Role == ChatRole.Assistant);
            return new(new ChatMessage(ChatRole.Assistant, "No matching session is listed for unicorn astrophysics in this schedule."));
        }
        var session = new ScheduleCorpus().Sessions.Single(s => s.Id == 13);
        return new(new ChatMessage(ChatRole.Assistant,
            $"{session.Title} - {session.When}, {session.Room}. " +
            $"A session about agentic AI. [#s13]({session.Citation})"));
    });

    internal static void AssertScheduleContext(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        var context = string.Join("\n", messages.Select(message => message.Text));
        Assert.Contains(ScheduleCorpus.LoadMarkdown(), context);
        Assert.Contains("## Day 1", context);
        Assert.Contains("## Day 2", context);
        Assert.All(new ScheduleCorpus().Sessions, session => Assert.Contains($"<a id=\"s{session.Id}\"></a>", context));
        Assert.Empty(options?.Tools ?? []);
        Assert.Null(options?.ResponseFormat);
        Assert.DoesNotContain(messages.SelectMany(message => message.Contents),
            content => content is FunctionCallContent or FunctionResultContent);
    }

    [Theory]
    [InlineData("Show 2 AI sessions on day 1")]
    [InlineData(".NET")]
    [InlineData("Inside the .NET Garbage Collector")]
    [InlineData("What about sightseeing in Prague?")]
    public async Task Query_numbers_and_leading_dots_cannot_drop_canonical_evidence(string question)
    {
        var calls = 0;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            calls++;
            AssertScheduleContext(messages.ToArray(), options);
            Assert.Contains(messages, message => message.Role == ChatRole.User && message.Text == question);
            return new(new ChatMessage(ChatRole.Assistant, "A grounded answer or explicit limitation."));
        });
        Assert.Equal("A grounded answer or explicit limitation.", await ScheduleDemo.RunAsync(client, question));
        Assert.Equal(1, calls);
    }
}
