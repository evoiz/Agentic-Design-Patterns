using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter21.ExplorationDiscovery;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter21.ExplorationDiscovery.Hosted;

public sealed class ExplorationHostedAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter21-exploration-discovery";
    public override string Description =>
        "Builds a bounded research exploration proposal and returns independent reviewer assessments.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var topic = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        var result = await ExplorationWorkflow.RunAsync(
            chatClient,
            topic,
            cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, result.Format()));
    }

    // Buffer the bounded review loop so the Responses protocol returns one complete, reviewed proposal.
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await RunCoreAsync(messages, session, options, cancellationToken);
        yield return new AgentResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent(response.Text)]
        };
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(
        CancellationToken cancellationToken = default) =>
        new(new ExplorationSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(new ExplorationSession());

    private sealed class ExplorationSession : AgentSession;
}
