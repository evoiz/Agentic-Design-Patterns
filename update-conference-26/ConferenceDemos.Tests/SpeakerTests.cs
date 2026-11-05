using AgenticDesignPatterns.TestSupport;
using ConferenceDemos;
using Microsoft.Extensions.AI;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class SpeakerTests
{
    private static SpeakerDraft ValidDraft() =>
        new([new("schedule-s9", "The relationship between the runtime, the SDK, the CI and the App")], false);

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task Real_harness_and_reviewer_use_read_only_sources_and_at_most_one_revision(bool reject, int revisions)
    {
        var reviewCalls = 0;
        var researchCalls = 0;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            if (IsReviewer(options))
            {
                reviewCalls++;
                Assert.Empty(options?.Tools ?? []);
                var input = string.Join("\n", messages.Select(m => m.Text));
                Assert.Contains("Sources", input);
                Assert.Contains("schedule-s9", input);
                return LocalDemoTests.Json(new SpeakerReview(!reject, reject ? "Replace the claim with a shorter exact quote." : "All citations resolve."));
            }
            researchCalls++;
            var tool = Assert.Single(options?.Tools ?? []);
            Assert.Equal("ReadSpeakerSources", tool.Name);
            var hasSource = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any();
            return !hasSource ? ToolCall("read-" + researchCalls, tool.Name) : LocalDemoTests.Json(ValidDraft());
        });

        var result = await SpeakerDemo.RunAsync(client, SpeakerDemo.DefaultRequest);
        Assert.Equal(revisions, result.Revisions);
        Assert.Equal(1, reviewCalls);
        Assert.Equal(2 + revisions * 2, researchCalls);
        Assert.Equal(1 + revisions, result.ToolCalls);
        Assert.Empty(SpeakerDemo.Validate(result.Draft, SpeakerDemo.Sources()));
        var text = SpeakerDemo.Format(result);
        Assert.Contains("No next talk was verified", text);
        Assert.Contains("https://prague.updateconference.net/", text);
        Assert.Contains("2026-11-05", text);
    }

    [Fact]
    public async Task Deterministic_evidence_errors_force_revision_even_if_reviewer_approves()
    {
        var drafts = 0;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            if (IsReviewer(options))
            {
                return LocalDemoTests.Json(new SpeakerReview(true, "Looks good."));
            }
            if (!messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any())
            {
                return ToolCall("read", Assert.Single(options?.Tools ?? []).Name);
            }
            return LocalDemoTests.Json(++drafts == 1 ? new SpeakerDraft([new("invented", "unsupported")], true) : ValidDraft());
        });
        var result = await SpeakerDemo.RunAsync(client, SpeakerDemo.DefaultRequest);
        Assert.Equal(1, result.Revisions);
        Assert.Equal(2, drafts);
    }

    [Fact]
    public async Task Invalid_revision_fails_instead_of_repeating_or_returning_success()
    {
        var drafts = 0;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            if (IsReviewer(options))
            {
                return LocalDemoTests.Json(new SpeakerReview(false, "Unsupported future-event claim."));
            }
            if (!messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any())
            {
                return ToolCall("read", Assert.Single(options?.Tools ?? []).Name);
            }
            drafts++;
            return LocalDemoTests.Json(new SpeakerDraft([new("missing-source", "invented")], true));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => SpeakerDemo.RunAsync(client, SpeakerDemo.DefaultRequest));
        Assert.Equal(2, drafts);
    }

    [Fact]
    public async Task Endless_tool_loop_hits_bounded_execution_and_cannot_complete()
    {
        var calls = 0;
        using var client = new ScriptedChatClient((_, options) =>
        {
            calls++;
            return ToolCall("read-" + calls, Assert.Single(options?.Tools ?? []).Name);
        });
        await Assert.ThrowsAnyAsync<Exception>(() => SpeakerDemo.RunAsync(client, SpeakerDemo.DefaultRequest));
        Assert.InRange(calls, 1, SpeakerDemo.MaximumResearchCalls);
    }

    [Fact]
    public async Task Reading_beyond_source_tool_budget_fails_even_if_model_later_claims_success()
    {
        var calls = 0;
        using var client = new ScriptedChatClient((_, options) =>
        {
            calls++;
            if (IsReviewer(options))
            {
                return LocalDemoTests.Json(new SpeakerReview(true, "Looks good."));
            }
            if (calls == 1)
            {
                return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    Enumerable.Range(1, 5).Select(i => (AIContent)new FunctionCallContent(
                        $"read-{i}", Assert.Single(options?.Tools ?? []).Name, new Dictionary<string, object?>())).ToList()));
            }
            return LocalDemoTests.Json(ValidDraft());
        });
        await Assert.ThrowsAnyAsync<Exception>(() => SpeakerDemo.RunAsync(client, SpeakerDemo.DefaultRequest));
    }

    [Fact]
    public async Task No_source_read_empty_request_and_cancellation_cannot_succeed()
    {
        using var client = new ScriptedChatClient((_, options) => IsReviewer(options)
            ? LocalDemoTests.Json(new SpeakerReview(true, "Approved."))
            : LocalDemoTests.Json(ValidDraft()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => SpeakerDemo.RunAsync(client, SpeakerDemo.DefaultRequest));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => SpeakerDemo.RunAsync(client, " "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => SpeakerDemo.RunAsync(client, new string('x', 4001)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SpeakerDemo.RunAsync(client, SpeakerDemo.DefaultRequest, new CancellationToken(true)));
    }

    [Fact]
    public void Citation_and_future_event_claims_are_checked_by_code()
    {
        var sources = SpeakerDemo.Sources();
        Assert.Empty(SpeakerDemo.Validate(ValidDraft(), sources));
        Assert.NotEmpty(SpeakerDemo.Validate(ValidDraft() with { FutureTalkVerified = true }, sources));
        Assert.NotEmpty(SpeakerDemo.Validate(new([new("schedule-s9", "private motivation invented here")], false), sources));
        Assert.NotEmpty(SpeakerDemo.Validate(new([new("unknown-source", ValidDraft().Claims[0].Quote)], false), sources));
        Assert.NotEmpty(SpeakerDemo.Validate(new([], false), sources));
    }

    private static bool IsReviewer(ChatOptions? options) =>
        options?.Instructions?.Contains("Review the draft", StringComparison.Ordinal) == true;

    private static ChatResponse ToolCall(string id, string name) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(id, name, new Dictionary<string, object?>())]));
}
