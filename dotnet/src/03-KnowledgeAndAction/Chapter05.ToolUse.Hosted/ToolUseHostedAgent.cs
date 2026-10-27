using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter05.ToolUse;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter05.ToolUse.Hosted;

public sealed class ToolUseHostedAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter05-tool-use";
    public override string Description =>
        "Selects and invokes a narrow lookup or calculator function to answer a caller-provided request.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var response = await ToolUseAgent.RunAsync(chatClient, request, cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, response));
    }

    // The Responses protocol supports streaming; buffer the completed tool run as one final update.
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

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        => new(new ToolUseSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new ToolUseSession());

    private sealed class ToolUseSession : AgentSession;
}
