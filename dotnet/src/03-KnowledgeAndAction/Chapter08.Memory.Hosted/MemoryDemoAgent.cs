using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Chapter08.Memory.Demos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter08.Memory.Hosted;

public sealed class MemoryDemoAgent : AIAgent
{
    public override string Name => "chapter08-memory";
    public override string Description =>
        "Demonstrates Foundry-managed memory by storing a synthetic travel preference and recalling it in a new MAF session.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text);

        var output = new StringBuilder();
        output.AppendLine("Scripted demo: caller messages are not written to managed memory. Each invocation uses an isolated temporary memory scope.");
        await FoundryMemoryDemo.RunAsync(
            writeOutput: line => output.AppendLine(line),
            cancellationToken: cancellationToken,
            useTemporaryScope: true);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, output.ToString()));
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
        => new(new MemoryDemoSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new MemoryDemoSession());

    private sealed class MemoryDemoSession : AgentSession;
}
