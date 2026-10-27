using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter17.Reasoning;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter17.Reasoning.Hosted;

public sealed class ReasoningHostedAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter17-reasoning";
    public override string Description =>
        "Runs a bounded reasoning scenario against caller-provided input without retaining conversation history.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var (scenario, input) = ParseRequest(request);
        var result = await ReasoningWorkflow.RunAsync(
            chatClient,
            scenario,
            input,
            cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, result));
    }

    // The Responses protocol supports streaming; buffer the completed reasoning run as one final update.
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
        new(new ReasoningSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(new ReasoningSession());

    public static (ReasoningScenario Scenario, string Input) ParseRequest(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var separator = request.IndexOf(':');
        if (separator > 0)
        {
            var mode = request[..separator].Trim().ToLowerInvariant();
            var input = request[(separator + 1)..].Trim();
            var scenario = mode switch
            {
                "cot" or "reason" => ReasoningScenario.ChainOfThought,
                "self-correction" or "selfcorrection" => ReasoningScenario.SelfCorrection,
                "react" => ReasoningScenario.ReAct,
                "deep-search" or "deepsearch" => ReasoningScenario.DeepSearch,
                _ => (ReasoningScenario?)null
            };
            if (scenario is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(input);
                return (scenario.Value, input);
            }
        }

        return (ReasoningScenario.ChainOfThought, request.Trim());
    }

    private sealed class ReasoningSession : AgentSession;
}
