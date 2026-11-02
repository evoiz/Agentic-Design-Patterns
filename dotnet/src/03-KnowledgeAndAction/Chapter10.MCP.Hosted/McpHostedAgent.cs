using AgenticDesignPatterns.Shared.Hosting;
using Chapter10.MCP;
using Microsoft.Extensions.AI;

namespace Chapter10.MCP.Hosted;

// Each request opens a fresh MCP connection; only the allow-listed greet tool reaches the model.
public sealed class McpHostedAgent(IChatClient chatClient, Uri mcpServerEndpoint) : StatelessWorkflowAgent
{
    public override string Name => "chapter10-mcp";
    public override string Description => "Uses a configured remote MCP server to answer the caller's request.";

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken)
    {
        await using var mcpClient = await McpAgent.ConnectToHttpServerAsync(mcpServerEndpoint, cancellationToken);
        return await McpAgent.RunAsync(chatClient, mcpClient, input, ["greet"], cancellationToken);
    }
}
