using Microsoft.Extensions.AI;

namespace Chapter03.Parallelization;

public static class ParallelizationDemo
{
    public static Task RunSustainableTechnologyAsync(IChatClient chatClient) =>
        RunAsync(
            chatClient,
            ParallelizationScenario.SustainableTechnology,
            "Summarize sustainable technology progress and climate solutions.",
            Console.WriteLine);

    public static Task RunTopicAnalysisAsync(IChatClient chatClient) =>
        RunAsync(
            chatClient,
            ParallelizationScenario.TopicAnalysis,
            "The history of space exploration",
            Console.WriteLine);

    public static async Task RunAsync(
        IChatClient chatClient,
        ParallelizationScenario scenario,
        string input,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default) =>
        _ = await ParallelizationWorkflow.RunAsync(
            chatClient,
            scenario,
            input,
            writeOutput,
            cancellationToken);
}
