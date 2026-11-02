using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent;

public static partial class MultiAgentWorkflow
{
    private static async Task<string> RunLoopAsync(
        IChatClient chatClient, string input, Action<string>? writeOutput, CancellationToken cancellationToken)
    {
        var processor = CreateAgent(chatClient, "IterativeProcessor", """
            Work on the user's task. Return a JSON object with:
            - isComplete: true only when the task has a usable completed result
            - result: the current result, including any useful partial work
            - nextStep: a short next action when incomplete, otherwise an empty string
            Use only facts you can support; do not claim external research.
            """);
        var progress = "No work has been done yet.";
        for (var iteration = 1; iteration <= MaxLoopIterations; iteration++)
        {
            var response = await processor.RunAsync<LoopProgress>(
                $"Task: {input}\n\nCurrent progress: {progress}\n\nIteration: {iteration} of {MaxLoopIterations}.",
                cancellationToken: cancellationToken);
            var step = response.Result
                ?? throw new InvalidOperationException("The loop agent returned no structured progress.");
            var result = WriteResult($"Loop iteration {iteration} of {MaxLoopIterations}", step.Result, writeOutput);
            if (step.IsComplete)
            {
                writeOutput?.Invoke("--- Completion check: complete ---");
                return result;
            }

            if (string.IsNullOrWhiteSpace(step.NextStep))
            {
                throw new InvalidOperationException("The loop agent marked its work incomplete without providing a next step.");
            }

            progress = $"{result}\nNext step: {step.NextStep}";
            writeOutput?.Invoke("--- Completion check: continue ---");
        }

        throw new InvalidOperationException(
            $"The loop reached its deterministic limit of {MaxLoopIterations} iterations without completion.");
    }

    private sealed record LoopProgress(bool IsComplete, string Result, string NextStep);
}
