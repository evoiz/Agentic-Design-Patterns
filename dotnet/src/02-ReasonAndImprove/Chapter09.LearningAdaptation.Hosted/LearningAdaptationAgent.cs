using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter09.LearningAdaptation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter09.LearningAdaptation.Hosted;

public sealed class LearningAdaptationAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter09-learning-adaptation";
    public override string Description =>
        "Scores a caller-provided prompt against a fixed benchmark and returns a revised proposal for human review.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var instructions = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        var result = await LearningAdaptationWorkflow.RunAsync(
            chatClient,
            instructions,
            maxIterations: 1,
            cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, result.Format()));
    }

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
        => new(new LearningAdaptationSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new LearningAdaptationSession());

    private sealed class LearningAdaptationSession : AgentSession;
}
