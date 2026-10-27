using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter10.MCP;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter10.MCP.Hosted;

public sealed class McpHostedAgent(IChatClient chatClient, Uri mcpServerEndpoint) : AIAgent
{
    public override string Name => "chapter10-mcp";
    public override string Description => "Uses a configured remote MCP server to answer the caller's request.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);

        await using var mcpClient = await McpAgent.ConnectToHttpServerAsync(
            mcpServerEndpoint,
            cancellationToken);
        var response = await McpAgent.RunAsync(
            chatClient,
            mcpClient,
            request,
            ["greet"],
            cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, response));
    }

    // The Responses protocol supports streaming; buffer the completed MCP tool run as one final update.
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
        => new(new McpSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new McpSession());

    private sealed class McpSession : AgentSession;
}
