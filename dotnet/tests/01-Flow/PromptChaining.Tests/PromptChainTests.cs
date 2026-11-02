using AgenticDesignPatterns.TestSupport;
using Chapter01.PromptChaining.Demos;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.PromptChaining.Tests;

public sealed class PromptChainTests
{
    [Fact]
    public async Task Sequential_workflow_preserves_intermediate_output_and_requests_a_schema()
    {
        var calls = 0;
        var output = new List<string>();
        using var client = new ScriptedChatClient((messages, options) =>
        {
            if (calls++ == 0)
            {
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Extracted facts."));
            }

            Assert.Contains(messages, message => message.Text == "Extracted facts.");
            Assert.NotNull(options?.ResponseFormat);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, """{"result":"Formatted facts."}"""));
        });

        var result = await PromptChainRunner.RunAsync<Payload>(
            client, "Source", "Extractor", "Extract", "Formatter", "Format", output.Add);

        Assert.Equal(2, calls);
        Assert.Contains("Formatted facts.", result);
        Assert.Contains(output, text => text.Contains("Extracted facts."));
    }

    [Theory]
    [InlineData("Not JSON.")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{"result":null}""")]
    public async Task Invalid_final_json_fails_explicitly(string output)
    {
        using var client = new ScriptedChatClient((_, _) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, output)));
        await Assert.ThrowsAnyAsync<Exception>(() => PromptChainRunner.RunAsync<Payload>(
            client, "Source", "Extractor", "Extract", "Formatter", "Format", _ => { }));
    }

    public sealed record Payload([property: System.Text.Json.Serialization.JsonRequired] string Result);

    [Fact]
    public async Task Cancellation_prevents_model_calls()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new ScriptedChatClient((_, _) =>
            throw new Xunit.Sdk.XunitException("Unexpected model call."));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PromptChainRunner.RunAsync<Payload>(
            client, "Source", "Extractor", "Extract", "Formatter", "Format", _ => { }, cancellation.Token));
    }

    [Fact]
    public async Task Provider_failure_is_not_returned_as_success()
    {
        using var client = new ScriptedChatClient((_, _) => throw new HttpRequestException("Model unavailable."));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => PromptChainRunner.RunAsync<Payload>(
            client, "Source", "Extractor", "Extract", "Formatter", "Format", _ => { }));
        Assert.Contains("Model unavailable.", exception.ToString());
    }
}
