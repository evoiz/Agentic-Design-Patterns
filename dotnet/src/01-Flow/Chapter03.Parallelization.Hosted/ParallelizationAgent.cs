using AgenticDesignPatterns.Shared.Hosting;
using Chapter03.Parallelization;
using Microsoft.Extensions.AI;

namespace Chapter03.Parallelization.Hosted;

public sealed class ParallelizationAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter03-parallelization";
    public override string Description =>
        "Runs independent model tasks in parallel, then synthesizes their results.";

    protected override Task<string> RunAsync(string request, CancellationToken cancellationToken)
    {
        var (scenario, input) = ParseRequest(request);
        return ParallelizationWorkflow.RunAsync(
            chatClient,
            scenario,
            input,
            cancellationToken: cancellationToken);
    }

    private static (ParallelizationScenario Scenario, string Input) ParseRequest(string request)
    {
        const string researchPrefix = "research:";
        const string topicPrefix = "topic:";

        if (request.StartsWith(researchPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var input = request[researchPrefix.Length..].Trim();
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            return (ParallelizationScenario.SustainableTechnology, input);
        }

        if (request.StartsWith(topicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var input = request[topicPrefix.Length..].Trim();
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            return (ParallelizationScenario.TopicAnalysis, input);
        }

        return (ParallelizationScenario.TopicAnalysis, request);
    }

}
