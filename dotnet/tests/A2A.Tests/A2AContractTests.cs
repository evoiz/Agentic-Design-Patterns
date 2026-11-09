using A2A;
using A2A.AspNetCore;
using AgenticDesignPatterns.TestSupport;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.AI;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace A2A.Tests;

public sealed class A2AContractTests
{
    [Theory]
    [InlineData("not a URL")]
    [InlineData("file:///agent")]
    public void Agent_endpoint_rejects_invalid_or_unsupported_urls(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => InterAgentAgent.ValidateEndpoint(endpoint));
    }

    [Fact]
    public async Task Discovered_card_advertises_both_bindings_and_client_runs_sync_and_streaming()
    {
        using var model = new ScriptedChatClient((messages, _) =>
        {
            var input = messages.Last(message => message.Role == ChatRole.User).Text;
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Handled: {input}"));
        });
        var agent = InterAgentAgent.Create(model);
        await using var host = await StartHostAsync(agent);
        var card = await new A2ACardResolver(host.AgentUrl).GetAgentCardAsync();

        Assert.Equal(InterAgentAgent.Name, card.Name);
        Assert.Contains("text/plain", card.DefaultInputModes);
        Assert.DoesNotContain("application/json", card.DefaultInputModes);
        Assert.Contains(card.SupportedInterfaces, binding =>
            binding.ProtocolBinding == ProtocolBindingNames.JsonRpc);
        Assert.Contains(card.SupportedInterfaces, binding =>
            binding.ProtocolBinding == ProtocolBindingNames.HttpJson);

        var syncAgent = card.AsAIAgent(options: new A2AClientOptions
        {
            PreferredBindings = [ProtocolBindingNames.JsonRpc]
        });
        var syncSession = await syncAgent.CreateSessionAsync();
        var sync = await syncAgent.RunAsync("Explain a weather request.", syncSession);
        Assert.Equal("Handled: Explain a weather request.", sync.Text);

        var streamAgent = card.AsAIAgent(options: new A2AClientOptions
        {
            PreferredBindings = [ProtocolBindingNames.HttpJson]
        });
        var streamSession = await streamAgent.CreateSessionAsync();
        var streamedText = new System.Text.StringBuilder();
        await foreach (var update in streamAgent.RunStreamingAsync(
            "Explain a second weather request.", streamSession))
        {
            streamedText.Append(update.Text);
        }

        Assert.Equal("Handled: Explain a second weather request.", streamedText.ToString());
    }

    [Fact]
    public async Task Model_failure_is_returned_as_a_failed_remote_request()
    {
        using var model = new ScriptedChatClient((_, _) =>
            throw new InvalidOperationException("scripted model failure"));
        await using var host = await StartHostAsync(InterAgentAgent.Create(model));
        var card = await new A2ACardResolver(host.AgentUrl).GetAgentCardAsync();
        var agent = card.AsAIAgent(options: new A2AClientOptions
        {
            PreferredBindings = [ProtocolBindingNames.JsonRpc]
        });
        var session = await agent.CreateSessionAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => agent.RunAsync("Weather in Paris?", session));
    }

    [Fact]
    public async Task Pre_cancelled_remote_call_does_not_invoke_the_model()
    {
        var called = false;
        using var model = new ScriptedChatClient((_, _) =>
        {
            called = true;
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "unused"));
        });
        await using var host = await StartHostAsync(InterAgentAgent.Create(model));
        var card = await new A2ACardResolver(host.AgentUrl).GetAgentCardAsync();
        var agent = card.AsAIAgent(options: new A2AClientOptions
        {
            PreferredBindings = [ProtocolBindingNames.JsonRpc]
        });
        var session = await agent.CreateSessionAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => agent.RunAsync("Weather in Paris?", session, cancellationToken: cancellation.Token));

        Assert.False(called);
    }

    private static async Task<LocalA2AHost> StartHostAsync(AIAgent agent)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var builder = WebApplication.CreateBuilder();
        var agentUrl = new Uri($"http://127.0.0.1:{port}/");
        builder.WebHost.UseUrls(agentUrl.ToString());
        builder.Services.AddHttpClient();
#pragma warning disable MEAI001
        builder.AddA2AServer(agent, options =>
        {
            options.AgentRunMode = AgentRunMode.ReturnTaskWhen(
                (context, _) => ValueTask.FromResult(context.RequestContext.StreamingResponse));
        });
#pragma warning restore MEAI001

        var app = builder.Build();
        var card = new AgentCard
        {
            Name = InterAgentAgent.Name,
            Description = InterAgentAgent.Description,
            Version = "1.0.0",
            DefaultInputModes = ["text/plain"],
            DefaultOutputModes = ["text/plain"],
            Capabilities = new AgentCapabilities { Streaming = true, PushNotifications = false },
            Skills =
            [
                new AgentSkill
                {
                    Id = "weather-guidance",
                    Name = "Weather guidance",
                    Description = "General guidance, not live observations.",
                    Tags = ["weather"],
                    Examples = ["What's the weather in Paris?"]
                }
            ],
            SupportedInterfaces =
            [
                new AgentInterface
                {
                    Url = agentUrl.ToString(),
                    ProtocolBinding = ProtocolBindingNames.HttpJson,
                    ProtocolVersion = "1.0"
                },
                new AgentInterface
                {
                    Url = agentUrl.ToString(),
                    ProtocolBinding = ProtocolBindingNames.JsonRpc,
                    ProtocolVersion = "1.0"
                }
            ]
        };
        app.MapA2AHttpJson(agent, "/");
        app.MapA2AJsonRpc(agent, "/");
        app.MapWellKnownAgentCard(card);
        await app.StartAsync();

        return new LocalA2AHost(app, agentUrl);
    }

    private sealed class LocalA2AHost(WebApplication app, Uri agentUrl) : IAsyncDisposable
    {
        public Uri AgentUrl { get; } = agentUrl;

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
