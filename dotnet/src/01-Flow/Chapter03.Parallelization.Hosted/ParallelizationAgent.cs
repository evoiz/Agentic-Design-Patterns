using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter03.Parallelization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter03.Parallelization.Hosted;

public sealed class ParallelizationAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter03-parallelization";
    public override string Description =>
        "Runs independent model tasks in parallel, then synthesizes their results.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var (scenario, input) = ParseRequest(request);
        var output = await ParallelizationWorkflow.RunAsync(
            chatClient,
            scenario,
            input,
            cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, output));
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
        => new(new ParallelizationSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new ParallelizationSession());

    private static (ParallelizationScenario Scenario, string Input) ParseRequest(string request)
    {
        const string researchPrefix = "research:";
        const string topicPrefix = "topic:";

        if (request.StartsWith(researchPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var input = request[researchPrefix.Length..].Trim();
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            return (ParallelizationScenario.SustainableTechnology, input);
        }

        if (request.StartsWith(topicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var input = request[topicPrefix.Length..].Trim();
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            return (ParallelizationScenario.TopicAnalysis, input);
        }

        return (ParallelizationScenario.TopicAnalysis, request);
    }

    private sealed class ParallelizationSession : AgentSession;
}
