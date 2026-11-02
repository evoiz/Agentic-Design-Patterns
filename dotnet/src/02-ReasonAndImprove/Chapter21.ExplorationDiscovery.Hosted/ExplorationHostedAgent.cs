using AgenticDesignPatterns.Shared.Hosting;
using Chapter21.ExplorationDiscovery;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter21.ExplorationDiscovery.Hosted;

public sealed class ExplorationHostedAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter21-exploration-discovery";
    public override string Description =>
        "Builds a bounded research exploration proposal and returns independent reviewer assessments.";

    protected override async Task<string> RunAsync(string topic, CancellationToken cancellationToken)
    {
        var result = await ExplorationWorkflow.RunAsync(
            chatClient,
            topic,
            cancellationToken: cancellationToken);
        return result.Format();
    }
}
