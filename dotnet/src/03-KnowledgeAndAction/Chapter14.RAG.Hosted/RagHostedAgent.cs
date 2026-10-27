using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter14.RAG;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter14.RAG.Hosted;

public sealed class RagHostedAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter14-rag";
    public override string Description =>
        "Answers caller questions using retrieved passages from the Chapter 14 sample knowledge base.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var question = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        var answer = await RagAgent.RunAsync(chatClient, question, cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, answer));
    }

    // The Responses protocol supports streaming; buffer the completed retrieval-and-answer run.
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
        => new(new RagSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new RagSession());

    private sealed class RagSession : AgentSession;
}
