using AgenticDesignPatterns.Shared.Hosting;
using Chapter17.Reasoning;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter17.Reasoning.Hosted;

public sealed class ReasoningHostedAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter17-reasoning";
    public override string Description =>
        "Runs a bounded reasoning scenario against caller-provided input without retaining conversation history.";

    protected override async Task<string> RunAsync(string request, CancellationToken cancellationToken)
    {
        var (scenario, input) = ParseRequest(request);
        return await ReasoningWorkflow.RunAsync(
            chatClient,
            scenario,
            input,
            cancellationToken: cancellationToken);
    }

    public static (ReasoningScenario Scenario, string Input) ParseRequest(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var separator = request.IndexOf(':');
        if (separator > 0)
        {
            var mode = request[..separator].Trim().ToLowerInvariant();
            var input = request[(separator + 1)..].Trim();
            var scenario = mode switch
            {
                "cot" or "reason" => ReasoningScenario.ChainOfThought,
                "self-correction" or "selfcorrection" => ReasoningScenario.SelfCorrection,
                "react" => ReasoningScenario.ReAct,
                "deep-search" or "deepsearch" => ReasoningScenario.DeepSearch,
                _ => (ReasoningScenario?)null
            };
            if (scenario is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(input);
                return (scenario.Value, input);
            }
        }

        return (ReasoningScenario.ChainOfThought, request.Trim());
    }

}
