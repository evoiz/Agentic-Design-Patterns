using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter18.Guardrails;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter18.Guardrails.Hosted;

public sealed class GuardrailsHostedAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter18-guardrails";
    public override string Description =>
        "Screens caller requests with a structured policy evaluator before allowing the assistant to respond.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var response = await GuardrailsAgent.Create(chatClient)
            .RunAsync(request, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException("The guarded assistant returned an empty response.");
        }

        return new AgentResponse(new ChatMessage(ChatRole.Assistant, response.Text));
    }

    // The Responses protocol supports streaming; buffer the policy-gated result as one final update.
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
        => new(new GuardrailsSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new GuardrailsSession());

    private sealed class GuardrailsSession : AgentSession;
}
