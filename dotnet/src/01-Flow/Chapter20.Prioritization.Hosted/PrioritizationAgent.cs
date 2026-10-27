using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter20.Prioritization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter20.Prioritization.Hosted;

public sealed class PrioritizationAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter20-prioritization";
    public override string Description =>
        "Proposes priorities for caller-supplied tasks and returns a deterministic priority queue.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var input = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var taskDescriptions = ParseTaskDescriptions(input);
        var schedule = await PrioritizationWorkflow.RunAsync(
            chatClient,
            taskDescriptions,
            cancellationToken: cancellationToken);
        var result = string.Join(
            Environment.NewLine,
            schedule.Select((task, index) =>
                $"{index + 1}. {task.Id} | {task.Priority} | {task.AssignedTo} | {task.Description}"));
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, result));
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
        => new(new PrioritizationSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new PrioritizationSession());

    private sealed class PrioritizationSession : AgentSession;

    private static IReadOnlyList<string> ParseTaskDescriptions(string input)
    {
        var trimmedInput = input.Trim();
        if (!trimmedInput.StartsWith('{'))
        {
            return trimmedInput.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        TaskBatchRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<TaskBatchRequest>(trimmedInput, JsonSerializerOptions.Web);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "JSON task requests must have the shape {\"tasks\":[\"description\", ...]}.",
                nameof(input),
                exception);
        }

        return request?.Tasks
            ?? throw new ArgumentException(
                "JSON task requests must include a non-null 'tasks' array.",
                nameof(input));
    }

    private sealed record TaskBatchRequest(IReadOnlyList<string>? Tasks);
}
