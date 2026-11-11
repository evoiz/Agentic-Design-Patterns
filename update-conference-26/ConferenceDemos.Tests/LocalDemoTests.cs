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

    [Theory]
    [InlineData("When is Cédric speaking on DAy 2?")]
    [InlineData("When is Cedric speaking on day 1?")]
    [InlineData("When is Cédric speaking on November 13?")]
    public async Task Hosted_speaker_questions_receive_the_complete_speaker_day_index(string question)
    {
        var corpus = new ScheduleCorpus();
        var calls = 0;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            calls++;
            var request = messages.ToArray();
            Assert.Contains(request, message => message.Role == ChatRole.User && message.Text == question);
            AssertScheduleContext(request, options);
            var context = string.Join("\n", request.Select(message => message.Text)).ReplaceLineEndings("\n");
            const string marker = "Complete speaker/day index, derived from every session in the snapshot:\n";
            var indexStart = context.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(indexStart >= 0);
            var indexJson = context[(indexStart + marker.Length)..].Split('\n', 2)[0];
            using var index = JsonDocument.Parse(indexJson);
            Assert.Equal(corpus.Sessions.Count, index.RootElement.GetArrayLength());
            foreach (var entry in index.RootElement.EnumerateArray())
            {
                var session = Assert.Single(corpus.Sessions, s => s.Id == entry.GetProperty("Id").GetInt32());
                Assert.Equal(session.Day, entry.GetProperty("Day").GetInt32());
                Assert.Equal(session.Speaker, entry.GetProperty("Speaker").GetString());
            }
            var speaker = Assert.Single(index.RootElement.EnumerateArray(),
                entry => entry.GetProperty("Speaker").GetString()?.Contains("Cédric", StringComparison.Ordinal) == true);
            Assert.Equal(1, speaker.GetProperty("Day").GetInt32());
            Assert.Equal(13, speaker.GetProperty("Id").GetInt32());
            return new(new ChatMessage(ChatRole.Assistant, "Scripted response; no live accuracy claim."));
        });

        await using var host = await HostedResponses.StartAsync(new ScheduleHostedAgent(client));
        using var result = await host.InvokeAsync(question, "update-schedule");
        Assert.Equal("completed", result.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("Is Cédric speaking on DAy 2?", "No.", "Day 2", true)]
    [InlineData("Is Cedric speaking on day 1?", "Yes.", "Day 1", true)]
    [InlineData("Is CÉDRIC MENDELIN speaking on November 13, 2026?", "No.", "Day 2", true)]
    [InlineData("Is Cedric speaking on November 12?", "Yes.", "Day 1", true)]
    [InlineData("Is Nonexistent Speaker speaking on day 2?", "No.", "Day 2", false)]
    public async Task Hosted_speaker_day_facts_are_answered_from_records_without_model_judgment(
        string question, string expectedStart, string requestedDay, bool hasAppearance)
    {
        using var client = new ScriptedChatClient((_, _) =>
            throw new InvalidOperationException("A speaker/day fact must not invoke the model."));
        await using var host = await HostedResponses.StartAsync(new ScheduleHostedAgent(client));
        using var result = await host.InvokeAsync(question, "update-schedule");
        Assert.Equal("completed", result.RootElement.GetProperty("status").GetString());
        var answer = HostedResponses.OutputText(result.RootElement);
        Assert.StartsWith(expectedStart, answer);
        Assert.Contains(requestedDay, answer);
        Assert.Equal(hasAppearance, answer.Contains("#s13", StringComparison.Ordinal));
        if (hasAppearance)
        {
            Assert.Contains("Day 1, November 12, 13:45", answer);
        }
        if (hasAppearance && expectedStart == "No.")
        {
            Assert.Contains("Listed on a different day:", answer);
            Assert.DoesNotContain("Yes.", answer);
        }
    }

    [Theory]
    [InlineData("Is Stefan Polz speaking on day 2?", "Yes.", "#s47")]
    [InlineData("Is Nilsson speaking on day 2?", "The name", "full speaker name")]
    [InlineData("Is Ced speaking on day 1?", "No.", "No appearance")]
    public async Task Speaker_lookup_handles_other_speakers_ambiguity_and_partial_names(
        string question, string expectedStart, string expectedDetail)
    {
        using var client = new ScriptedChatClient((_, _) =>
            throw new InvalidOperationException("No model judgment expected."));
        var answer = await ScheduleDemo.RunAsync(client, question);
        Assert.StartsWith(expectedStart, answer);
        Assert.Contains(expectedDetail, answer);
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
