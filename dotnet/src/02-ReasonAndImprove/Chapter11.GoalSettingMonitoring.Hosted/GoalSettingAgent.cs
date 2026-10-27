using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter11.GoalSettingMonitoring;
using Chapter11.GoalSettingMonitoring.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter11.GoalSettingMonitoring.Hosted;

public sealed class GoalSettingAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter11-goal-setting-monitoring";
    public override string Description =>
        "Generates code and iterates against caller-defined success goals within a fixed budget.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var input = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        var request = ParseRequest(input);
        var result = await GoalSettingWorkflow.RunAsync(
            chatClient,
            request,
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
        => new(new RequestSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new RequestSession());

    private static GoalSettingRequest ParseRequest(string? input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var trimmed = input.Trim();
        if (!trimmed.StartsWith('{'))
        {
            var request = GoalSettingRequest.BinaryGapSample with { UseCase = trimmed };
            request.Validate();
            return request;
        }

        GoalSettingRequest? structuredRequest;
        try
        {
            structuredRequest = JsonSerializer.Deserialize<GoalSettingRequest>(
                trimmed,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "A structured request must be valid JSON with useCase, goals, and optional maxIterations.",
                nameof(input),
                exception);
        }

        if (structuredRequest is null)
        {
            throw new ArgumentException("The structured request cannot be JSON null.", nameof(input));
        }

        structuredRequest.Validate();
        return structuredRequest;
    }

    private sealed class RequestSession : AgentSession;
}
