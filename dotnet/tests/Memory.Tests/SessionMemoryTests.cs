using Memory.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace Memory.Tests;

public sealed class SessionMemoryTests
{
    [Fact]
    public async Task Explicit_facts_and_last_response_survive_session_serialization_and_restore()
    {
        using var model = new RecordingChatClient("I will remember your preference.");
        var provider = new ConversationMemoryProvider();
        var agent = CreateAgent(model, provider);
        var session = await agent.CreateSessionAsync();
        provider.Remember(session, "seat", "window");

        await agent.RunAsync("I prefer window seats.", session);
        var serialized = await agent.SerializeSessionAsync(session);
        var restored = await agent.DeserializeSessionAsync(serialized);
        var state = provider.GetState(restored);

        Assert.Equal("window", state.Facts["seat"]);
        Assert.Equal("I will remember your preference.", state.LastResponse);
        Assert.Contains(
            "I will remember your preference.",
            (await agent.RunAsync("What seat did I prefer?", restored)).Text);
        Assert.Contains("window", model.LastRequest);
    }

    [Fact]
    public async Task New_sessions_are_isolated_from_previous_explicit_memory()
    {
        using var model = new RecordingChatClient("No preference is stored in this session.");
        var provider = new ConversationMemoryProvider();
        var agent = CreateAgent(model, provider);
        var first = await agent.CreateSessionAsync();
        var second = await agent.CreateSessionAsync();
        provider.Remember(first, "seat", "window");

        Assert.Equal("window", provider.GetState(first).Facts["seat"]);
        Assert.Empty(provider.GetState(second).Facts);

        await agent.RunAsync("What seat do I prefer?", second);
        Assert.DoesNotContain("window", model.LastRequest);
    }

    [Fact]
    public async Task Session_demo_rejects_an_empty_model_response()
    {
        using var model = new RecordingChatClient(" ");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SessionMemoryDemo.RunAsync(model));

        Assert.Contains("empty response", error.Message);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task Session_demo_honors_cancellation_before_model_calls()
    {
        using var model = new RecordingChatClient("unused");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SessionMemoryDemo.RunAsync(model, cancellationToken: cancellation.Token));

        Assert.Empty(model.Requests);
    }

    private static AIAgent CreateAgent(IChatClient chatClient, ConversationMemoryProvider provider) =>
        new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "MemoryTestAgent",
                AIContextProviders = [provider]
            });

    private sealed class RecordingChatClient(string response) : IChatClient
    {
        public List<string> Requests { get; } = [];
        public string LastRequest => Requests.Last();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prompt = string.Join("\n", messages.Select(message => message.Text));
            Requests.Add(prompt);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
