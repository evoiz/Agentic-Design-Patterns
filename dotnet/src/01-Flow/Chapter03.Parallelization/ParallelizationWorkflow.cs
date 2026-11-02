using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter03.Parallelization;

public enum ParallelizationScenario
{
    SustainableTechnology,
    TopicAnalysis
}

public sealed record ParallelizationWorker(string Name, string DisplayName, string Instructions);

public static class ParallelizationWorkflow
{
    public const string TelemetrySourceName = "Chapter03.Parallelization";

    public static async Task<string> RunAsync(
        IChatClient chatClient,
        ParallelizationScenario scenario,
        string input,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        cancellationToken.ThrowIfCancellationRequested();

        var workflowClient = new BufferedWorkflowChatClient(chatClient);
        var workerDefinitions = GetWorkers(scenario);
        var workers = workerDefinitions
            .Select(worker => CreateAgent(workflowClient, worker))
            .ToList();
        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            workers,
            AggregateOutputs);

        await using var run = await InProcessExecution.RunAsync(
            workflow,
            new ChatMessage(ChatRole.User, input),
            cancellationToken: cancellationToken);

        var parallelResults = GetWorkflowOutput(run.OutgoingEvents, workerDefinitions);
        for (var index = 0; index < parallelResults.Count; index++)
        {
            var result = parallelResults[index];
            writeOutput?.Invoke($"\n--- {workerDefinitions[index].DisplayName} (parallel branch) ---");
            writeOutput?.Invoke(result.Text ?? string.Empty);
        }

        var synthesisAgent = CreateAgent(workflowClient, GetSynthesizer(scenario));
        var synthesisResponse = await synthesisAgent.RunAsync(
            CreateSynthesisInput(scenario, input, parallelResults),
            cancellationToken: cancellationToken);

        writeOutput?.Invoke("\n--- Final synthesized response ---");
        writeOutput?.Invoke(synthesisResponse.Text);

        return !string.IsNullOrWhiteSpace(synthesisResponse.Text)
            ? synthesisResponse.Text
            : throw new InvalidOperationException("The parallelization workflow completed without a synthesized response.");
    }

    internal static IReadOnlyList<ParallelizationWorker> GetWorkers(ParallelizationScenario scenario) =>
        scenario switch
        {
            ParallelizationScenario.SustainableTechnology =>
            [
                new(
                    "RenewableEnergyResearcher",
                    "Renewable energy findings",
                    """
                    You are an AI research assistant specializing in energy.
                    Analyze the user's request through the lens of renewable energy sources.
                    Summarize your key findings concisely in 1-2 sentences. Output only the summary.
                    No web-search tool is available: do not claim to have searched the web or verified the latest developments.
                    """),
                new(
                    "EVResearcher",
                    "Electric vehicle findings",
                    """
                    You are an AI research assistant specializing in transportation.
                    Analyze the user's request through the lens of electric vehicle technology.
                    Summarize your key findings concisely in 1-2 sentences. Output only the summary.
                    No web-search tool is available: do not claim to have searched the web or verified the latest developments.
                    """),
                new(
                    "CarbonCaptureResearcher",
                    "Carbon capture findings",
                    """
                    You are an AI research assistant specializing in climate solutions.
                    Analyze the user's request through the lens of carbon capture methods.
                    Summarize your key findings concisely in 1-2 sentences. Output only the summary.
                    No web-search tool is available: do not claim to have searched the web or verified the latest developments.
                    """)
            ],
            ParallelizationScenario.TopicAnalysis =>
            [
                new(
                    "TopicSummarizer",
                    "Topic summary",
                    "Summarize the following topic concisely."),
                new(
                    "QuestionGenerator",
                    "Related questions",
                    "Generate three interesting questions about the following topic."),
                new(
                    "KeyTermExtractor",
                    "Key terms",
                    "Identify 5-10 key terms from the following topic, separated by commas.")
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown parallelization scenario.")
        };

    internal static ParallelizationWorker GetSynthesizer(ParallelizationScenario scenario) =>
        scenario switch
        {
            ParallelizationScenario.SustainableTechnology => new(
                "SynthesisAgent",
                "Sustainable technology synthesis",
                """
                You combine research summaries into a structured report with clear attribution.
                Ground the entire response exclusively in the provided input summaries. Do not add external facts.
                Use these headings:
                ## Summary of Sustainable Technology Findings
                ### Renewable Energy Findings
                ### Electric Vehicle Findings
                ### Carbon Capture Findings
                ### Overall Conclusion
                Keep the conclusion to 1-2 sentences and use only the supplied summaries.
                Output only the structured report.
                """),
            ParallelizationScenario.TopicAnalysis => new(
                "TopicSynthesisAgent",
                "Topic analysis synthesis",
                """
                Synthesize the supplied summary, related questions, and key terms into a comprehensive answer.
                Keep the result focused on the original topic and do not introduce unsupported specifics.
                """),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown parallelization scenario.")
        };

    internal static List<ChatMessage> AggregateOutputs(IList<List<ChatMessage>> outputs)
    {
        var responses = outputs
            .SelectMany(output => output)
            .Where(message => message.Role == ChatRole.Assistant)
            .ToList();
        return responses.Count > 0
            ? responses
            : throw new InvalidOperationException("Parallel workers completed without assistant responses.");
    }

    internal static List<ChatMessage> GetWorkflowOutput(
        IEnumerable<WorkflowEvent> workflowEvents,
        IReadOnlyList<ParallelizationWorker> workers)
    {
        List<ChatMessage>? responses = null;

        foreach (var workflowEvent in workflowEvents)
        {
            if (workflowEvent is WorkflowOutputEvent outputEvent)
            {
                responses = outputEvent.As<List<ChatMessage>>();
            }
            else if (workflowEvent is ExecutorFailedEvent failedEvent)
            {
                var exception = failedEvent.Data as Exception;
                throw new InvalidOperationException(
                    $"Parallel worker '{failedEvent.ExecutorId}' failed: " +
                    (exception?.Message ?? "No exception details were provided."),
                    exception);
            }
            else if (workflowEvent is WorkflowErrorEvent errorEvent)
            {
                throw new InvalidOperationException(
                    errorEvent.Exception is null
                        ? "The parallelization workflow reported an error without exception details."
                        : $"The parallelization workflow failed: {errorEvent.Exception.Message}",
                    errorEvent.Exception);
            }
        }

        if (responses is null)
        {
            throw new InvalidOperationException(
                "The parallelization workflow completed without producing a fan-in output.");
        }

        var results = new List<ChatMessage>(workers.Count);
        foreach (var worker in workers)
        {
            var response = responses.LastOrDefault(message =>
                FindWorker(message.AuthorName ?? string.Empty, workers) == worker)?.Text;

            if (string.IsNullOrWhiteSpace(response))
            {
                throw new InvalidOperationException(
                    $"Parallel worker '{worker.Name}' completed without an attributable assistant response.");
            }

            results.Add(new ChatMessage(
                ChatRole.Assistant,
                $"[{worker.DisplayName}]\n{response}"));
        }

        return results;
    }

    private static ParallelizationWorker? FindWorker(
        string executorId,
        IReadOnlyList<ParallelizationWorker> workers) =>
        workers.FirstOrDefault(worker =>
            string.Equals(executorId, worker.Name, StringComparison.Ordinal) ||
            executorId.StartsWith($"{worker.Name}_", StringComparison.Ordinal));

    internal static string CreateSynthesisInput(
        ParallelizationScenario scenario,
        string input,
        IReadOnlyList<ChatMessage> parallelResults)
    {
        var results = string.Join(
            "\n\n",
            parallelResults.Select(message => message.Text));

        return scenario switch
        {
            ParallelizationScenario.SustainableTechnology =>
                $"User request: {input}\n\nInput summaries:\n{results}",
            ParallelizationScenario.TopicAnalysis =>
                $"Original topic: {input}\n\n{results}",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown parallelization scenario.")
        };
    }

    private static AIAgent CreateAgent(IChatClient chatClient, ParallelizationWorker worker) =>
        new ChatClientAgent(chatClient, worker.Instructions, name: worker.Name)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
}
