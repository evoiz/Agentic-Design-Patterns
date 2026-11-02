using AgenticDesignPatterns.Shared.Hosting;
using AgenticDesignPatterns.TestSupport;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenticDesignPatterns.Shared.Tests;

public sealed class HostingTests
{
    [Fact]
    public async Task Stateless_adapter_uses_latest_user_input_and_roundtrips_empty_sessions()
    {
        var inputs = new List<string>();
        var agent = new EchoAgent((input, _) =>
        {
            inputs.Add(input);
            return Task.FromResult(input);
        });
        var session = await agent.CreateSessionAsync();
        await agent.RunAsync("first", session);
        var saved = await agent.SerializeSessionAsync(session);
        var restored = await agent.DeserializeSessionAsync(saved);
        var updates = new List<string>();
        await foreach (var update in agent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "old"), new ChatMessage(ChatRole.Assistant, "ignored"),
             new ChatMessage(ChatRole.User, "second")], restored))
        {
            updates.Add(update.Text);
        }

        Assert.Equal(["first", "second"], inputs);
        Assert.Equal(["second"], updates);
    }

    [Fact]
    public async Task Adapter_rejects_missing_input_empty_output_and_propagates_failures()
    {
        var echo = new EchoAgent((input, _) => Task.FromResult(input));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => echo.RunAsync(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            echo.RunAsync([new ChatMessage(ChatRole.Assistant, "not a request")]));
        var empty = new EchoAgent((_, _) => Task.FromResult(""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => empty.RunAsync("input"));
        var failure = new EchoAgent((_, _) => throw new HttpRequestException("provider unavailable"));
        await Assert.ThrowsAsync<HttpRequestException>(() => failure.RunAsync("input"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            echo.RunAsync("input", cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Responses_endpoint_returns_the_adapter_result()
    {
        await using var host = await HostedResponses.StartAsync(
            new EchoAgent((input, _) => Task.FromResult("result: " + input)));
        using var response = await host.InvokeAsync("caller input", "echo");
        Assert.Equal("completed", response.RootElement.GetProperty("status").GetString());
        Assert.Equal("result: caller input", HostedResponses.OutputText(response.RootElement));
    }

    [Fact]
    public async Task Scripted_client_preserves_tool_content_in_buffered_updates()
    {
        using var client = new ScriptedChatClient((_, _) => new ChatResponse(
            new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("call-1", "Lookup", new Dictionary<string, object?> { ["id"] = 42 })])));
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "lookup")]))
        {
            updates.Add(update);
        }

        var call = Assert.Single(updates.SelectMany(update => update.Contents).OfType<FunctionCallContent>());
        Assert.Equal("Lookup", call.Name);
        Assert.Equal("call-1", call.CallId);
        Assert.Equal(42, call.Arguments!["id"]);
    }

    [Fact]
    public void Hosted_settings_accept_runtime_names_and_deployment_aliases_without_an_api_key()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FOUNDRY_PROJECT_ENDPOINT"] = "https://example.services.ai.azure.com/api/projects/example",
            ["AZURE_AI_MODEL_DEPLOYMENT_NAME"] = "default-model",
            ["Foundry:ModelDeployments:fast"] = "fast-model"
        }).Build();
        var settings = HostedFoundryConfiguration.LoadSettings(configuration);
        Assert.Empty(settings.ApiKey);
        Assert.Equal("fast-model", settings.ResolveModelDeployment("FAST"));
        Assert.Equal("default-model", settings.ResolveModelDeployment());
        Assert.Throws<InvalidOperationException>(() =>
            HostedFoundryConfiguration.LoadSettings(new ConfigurationBuilder().Build()));
    }

    private sealed class EchoAgent(Func<string, CancellationToken, Task<string>> respond) : StatelessWorkflowAgent
    {
        public override string Name => "echo";
        protected override Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
            respond(input, cancellationToken);
    }
}
