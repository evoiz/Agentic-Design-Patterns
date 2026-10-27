using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter07.MultiAgent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent.Hosted;

public sealed class MultiAgentHostedAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter07-multi-agent";
    public override string Description =>
        "Runs one caller-selected multi-agent collaboration scenario without persisting conversation history.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var (scenario, input) = ParseRequest(request);
        var response = await MultiAgentWorkflow.RunAsync(
            chatClient,
            scenario,
            input,
            cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, response));
    }

    // The Responses protocol supports streaming; buffer the completed multi-agent run as one final update.
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
        new(new MultiAgentSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(new MultiAgentSession());

    public static (MultiAgentScenario Scenario, string Input) ParseRequest(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var separator = request.IndexOf(':');
        if (separator > 0 &&
            MultiAgentWorkflow.TryParseScenario(request[..separator].Trim(), out var scenario))
        {
            var input = request[(separator + 1)..].Trim();
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            return (scenario, input);
        }

        return (MultiAgentScenario.Crew, request.Trim());
    }

    private sealed class MultiAgentSession : AgentSession;
}
