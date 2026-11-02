using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgenticDesignPatterns.Shared.Hosting;

public abstract class StatelessWorkflowAgent : AIAgent
{
    protected abstract Task<string> RunAsync(string input, CancellationToken cancellationToken);

    protected virtual string GetInput(ChatMessage message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Text);
        return message.Text;
    }

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var message = messages.LastOrDefault(message => message.Role == ChatRole.User)
            ?? throw new ArgumentException("Supply a user request.", nameof(messages));
        var input = GetInput(message);
        var output = await RunAsync(input, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException($"Agent '{Name}' returned no output.");
        }

        return new AgentResponse(new ChatMessage(ChatRole.Assistant, output));
    }

    // The Responses protocol requires streaming; workflows return one completed result.
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
        => new(new WorkflowSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new WorkflowSession());

    private sealed class WorkflowSession : AgentSession;
}
