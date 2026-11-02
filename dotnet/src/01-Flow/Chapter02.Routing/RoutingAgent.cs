using AgenticDesignPatterns.Shared.Hosting;
using Microsoft.Extensions.AI;

namespace Chapter02.Routing;

public sealed class RoutingAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter02-routing";
    public override string Description => "Routes travel bookings and information questions to specialists; bookings are simulated.";

    protected override Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
        RoutingWorkflow.RunAsync(chatClient, input, cancellationToken: cancellationToken);
}
