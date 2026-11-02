using AgenticDesignPatterns.Shared.Hosting;
using Microsoft.Extensions.AI;

namespace Chapter05.ToolUse.Hosted;

public sealed class ToolUseHostedAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter05-tool-use";
    public override string Description =>
        "Selects and invokes a narrow lookup or calculator function to answer a caller-provided request.";

    protected override Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
        ToolUseAgent.RunAsync(chatClient, input, cancellationToken: cancellationToken);
}
