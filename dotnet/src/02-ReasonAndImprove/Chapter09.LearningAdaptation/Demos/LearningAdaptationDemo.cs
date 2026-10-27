using Chapter09.LearningAdaptation.Models;
using Microsoft.Extensions.AI;

namespace Chapter09.LearningAdaptation.Demos;

public static class LearningAdaptationDemo
{
    public const string InitialInstructions =
        "You are a helpful customer-support assistant. Answer the customer's question briefly.";

    public static Task<LearningAdaptationResult> RunAsync(
        IChatClient chatClient,
        int maxIterations = 1,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default) =>
        LearningAdaptationWorkflow.RunAsync(
            chatClient,
            InitialInstructions,
            maxIterations,
            writeOutput,
            cancellationToken);
}
