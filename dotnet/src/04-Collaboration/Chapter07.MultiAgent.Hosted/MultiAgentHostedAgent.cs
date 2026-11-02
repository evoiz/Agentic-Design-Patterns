using AgenticDesignPatterns.Shared.Hosting;
using Chapter07.MultiAgent;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent.Hosted;

public sealed class MultiAgentHostedAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter07-multi-agent";
    public override string Description =>
        "Runs one caller-selected multi-agent collaboration scenario without persisting conversation history.";

    protected override Task<string> RunAsync(string request, CancellationToken cancellationToken)
    {
        var (scenario, input) = ParseRequest(request);
        return MultiAgentWorkflow.RunAsync(
            chatClient,
            scenario,
            input,
            cancellationToken: cancellationToken);
    }

    public static (MultiAgentScenario Scenario, string Input) ParseRequest(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var separator = request.IndexOf(':');
        if (separator > 0 &&
            MultiAgentWorkflow.TryParseScenario(request[..separator].Trim(), out var scenario))
        {
            var input = request[(separator + 1)..].Trim();
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            return (scenario, input);
        }

        return (MultiAgentScenario.Crew, request.Trim());
    }

}
