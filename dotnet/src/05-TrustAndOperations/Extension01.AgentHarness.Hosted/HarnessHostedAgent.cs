using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Extension01.AgentHarness.Hosted;

public sealed class HarnessHostedAgent(IChatClient chatClient, bool enableSensitiveTelemetry = false) : AIAgent
{
    public override string Name => "extension01-agent-harness";
    public override string Description => "Bounded read-only support triage with session todos, evidence, and explicit completion status.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var result = await HarnessRunner.RunAsync(
            chatClient, request, enableSensitiveTelemetry: enableSensitiveTelemetry, cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant,
            JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }

    // Responses supports streaming; buffer the complete harness result instead of streaming model/tool intermediates.
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await RunCoreAsync(messages, session, options, cancellationToken);
        yield return new AgentResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent(result.Text)] };
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default) =>
        new(new StatelessSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) =>
        new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) =>
        new(new StatelessSession());

    private sealed class StatelessSession : AgentSession;
}
