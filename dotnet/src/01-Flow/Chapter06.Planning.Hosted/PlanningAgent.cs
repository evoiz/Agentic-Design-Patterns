using AgenticDesignPatterns.Shared.Hosting;
using Microsoft.Extensions.AI;

namespace Chapter06.Planning.Hosted;

public sealed class PlanningAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter06-planning";
    public override string Description =>
        "Creates a bounded plan for a topic, then writes a concise summary following that plan.";

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken)
    {
        var (planner, writer) = PlanningAgents.Create(chatClient);
        var result = await PlanningWorkflow.RunAsync(
            planner, writer, input, cancellationToken: cancellationToken);
        return result.Format();
    }
}
