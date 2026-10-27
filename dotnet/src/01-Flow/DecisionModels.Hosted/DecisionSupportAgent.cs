using System.Runtime.CompilerServices;
using System.Text.Json;
using ElBruno.AI.Decisions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DecisionModels.Hosted;

public sealed class DecisionSupportAgent(IDecisionClient decisions, IChatClient chat) : AIAgent
{
    public override string Name => "decision-models";
    public override string Description => "Typed support triage, priority, and review gates before a specialist draft.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null,
        AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        var message = messages.LastOrDefault(message => message.Role == ChatRole.User)
            ?? throw new ArgumentException("Supply a user support request.", nameof(messages));
        if (message.Contents.Any(content => content is not TextContent))
        {
            throw new ArgumentException("This agent accepts text-only support requests.", nameof(messages));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Text);
        var result = await DecisionAgentWorkflow.RunAsync(
            decisions, chat, message.Text, cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(result)));
    }

    // Responses can request a stream; publish only the completed, policy-checked result.
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await RunCoreAsync(messages, session, options, cancellationToken);
        yield return new AgentResponseUpdate
        {
            Role = ChatRole.Assistant, Contents = [new TextContent(response.Text)]
        };
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default) =>
        new(new StatelessSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) => new(new StatelessSession());

    private sealed class StatelessSession : AgentSession;
}
