using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent;

public static partial class MultiAgentWorkflow
{
    private static async Task<string> RunParallelAsync(
        IChatClient chatClient, string input, Action<string>? writeOutput, CancellationToken cancellationToken)
    {
        var workers = new[]
        {
            CreateAgent(chatClient, "WeatherAnalyst", """
                Analyze the request from a weather perspective. Do not claim live weather access.
                Clearly label any illustrative or model-generated estimate as unverified.
                """),
            CreateAgent(chatClient, "NewsAnalyst", """
                Analyze the request from a news and current-events perspective.
                No web-search tool is available; do not claim to fetch or verify current news.
                """)
        };
        var workflow = AgentWorkflowBuilder.BuildConcurrent(workers,
            outputs => outputs.SelectMany(output => output)
                .Where(message => message.Role == ChatRole.Assistant).ToList());
        await using var run = await InProcessExecution.RunAsync(
            workflow, new ChatMessage(ChatRole.User, input), cancellationToken: cancellationToken);
        List<ChatMessage>? results = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case WorkflowOutputEvent output:
                    results = output.As<List<ChatMessage>>();
                    break;
                case ExecutorFailedEvent failed:
                    throw new InvalidOperationException(
                        $"Parallel agent '{failed.ExecutorId}' failed: {failed.Data}", failed.Data as Exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("The parallel multi-agent workflow failed.", error.Exception);
            }
        }

        if (results is null)
        {
            throw new InvalidOperationException("The parallel workflow did not return specialist responses.");
        }

        var notes = workers.Select(worker =>
        {
            var message = results.FirstOrDefault(message =>
                message.AuthorName == worker.Name ||
                message.AuthorName?.StartsWith($"{worker.Name}_", StringComparison.Ordinal) == true);
            return WriteResult($"{worker.Name} (parallel branch)", message?.Text, writeOutput);
        }).ToArray();
        var synthesizer = CreateAgent(chatClient, "Coordinator",
            "Combine the specialist notes into a useful answer. Preserve uncertainty and do not add unsupported facts.");
        var synthesis = await synthesizer.RunAsync(
            $"Request: {input}\n\nWeather analysis:\n{notes[0]}\n\nNews analysis:\n{notes[1]}",
            cancellationToken: cancellationToken);
        return WriteResult("Coordinated result", synthesis.Text, writeOutput);
    }
}
