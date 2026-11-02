using AgenticDesignPatterns.Shared.Hosting;
using Microsoft.Extensions.AI;

namespace Chapter09.LearningAdaptation.Hosted;

public sealed class LearningAdaptationAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter09-learning-adaptation";
    public override string Description =>
        "Scores a caller-provided prompt against a fixed benchmark and returns a revised proposal for human review.";

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
        (await LearningAdaptationWorkflow.RunAsync(chatClient, input, maxIterations: 1, cancellationToken: cancellationToken))
            .Format();
}
