using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter12.ExceptionHandling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter12.ExceptionHandling.Hosted;

public sealed class ExceptionHandlingAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter12-exception-handling";
    public override string Description =>
        "Looks up a location precisely, retries bounded transient failures, and falls back to general-area information.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var output = await ExceptionHandlingWorkflow.RunAsync(
            chatClient,
            request,
            cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, output));
    }

    // The Responses protocol can request streaming; emit only the completed workflow result.
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
        => new(new ExceptionHandlingSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new ExceptionHandlingSession());

    private sealed class ExceptionHandlingSession : AgentSession;
}
