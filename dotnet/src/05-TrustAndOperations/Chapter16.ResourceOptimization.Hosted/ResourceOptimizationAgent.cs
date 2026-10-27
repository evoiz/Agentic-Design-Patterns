using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter16.ResourceOptimization;
using Chapter16.ResourceOptimization.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter16.ResourceOptimization.Hosted;

public sealed class ResourceOptimizationAgent(
    IChatClient classifierClient,
    IReadOnlyDictionary<OptimizationCategory, OptimizationModelClient> modelClients,
    AIAgent webSearchAgent) : AIAgent
{
    private readonly AIAgent _webSearchAgent = webSearchAgent.AsBuilder()
        .UseOpenTelemetry(
            ResourceOptimizationWorkflow.TelemetrySourceName,
            configure: options => options.EnableSensitiveData = true)
        .Build();

    public override string Name => "chapter16-resource-optimization";
    public override string Description =>
        "Classifies each request and routes it to a suitable model, using Foundry web search for current information.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var result = await ResourceOptimizationWorkflow.RunAsync(
            request,
            RoutingStrategy.IntentClassification,
            classifierClient,
            modelClients,
            cancellationToken: cancellationToken,
            webSearchAgent: _webSearchAgent);

        var response = $"""
            Classification: {result.Category}
            Model deployment: {result.ModelDeployment}
            Token usage: input={result.InputTokens?.ToString() ?? "not reported"}, output={result.OutputTokens?.ToString() ?? "not reported"}, total={result.TotalTokens?.ToString() ?? "not reported"}
            Answer:
            {result.Answer}
            """;
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, response))
        {
            Usage = result.InputTokens is null && result.OutputTokens is null && result.TotalTokens is null
                ? null
                : new UsageDetails
                {
                    InputTokenCount = result.InputTokens,
                    OutputTokenCount = result.OutputTokens,
                    TotalTokenCount = result.TotalTokens
                }
        };
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await RunCoreAsync(messages, session, options, cancellationToken);
        foreach (var update in response.ToAgentResponseUpdates())
        {
            yield return update;
        }
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(
        CancellationToken cancellationToken = default) =>
        new(new ResourceOptimizationSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) =>
        new(new ResourceOptimizationSession());

    private sealed class ResourceOptimizationSession : AgentSession;
}
