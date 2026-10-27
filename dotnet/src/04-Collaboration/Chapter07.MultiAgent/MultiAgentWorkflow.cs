using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent;

public enum MultiAgentScenario
{
    Sequential,
    Parallel,
    Loop,
    Coordinator,
    AgentTool,
    Crew
}

public static class MultiAgentWorkflow
{
    public const string TelemetrySourceName = "Chapter07.MultiAgent";
    public const int MaxLoopIterations = 3;

    public static async Task<string> RunAsync(
        IChatClient chatClient,
        MultiAgentScenario scenario,
        string input,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        cancellationToken.ThrowIfCancellationRequested();

        return scenario switch
        {
            MultiAgentScenario.Sequential =>
                await RunSequentialAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.Parallel =>
                await RunParallelAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.Loop =>
                await RunLoopAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.Coordinator =>
                await RunCoordinatorAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.AgentTool =>
                await RunAgentToolAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.Crew =>
                await RunCrewAsync(chatClient, input, writeOutput, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown multi-agent scenario.")
        };
    }

    public static bool TryParseScenario(string? value, out MultiAgentScenario scenario)
    {
        var normalized = value?.Replace("-", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse(normalized, ignoreCase: true, out scenario) &&
            Enum.IsDefined(scenario))
        {
            return true;
        }

        scenario = default;
        return false;
    }

    private static async Task<string> RunSequentialAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var researcher = CreateAgent(
            chatClient,
            "Researcher",
            """
            Summarize relevant background for the requested topic in three concise points.
            Work from general model knowledge only. Do not claim live web research or invent sources.
            """);
        var writer = CreateAgent(
            chatClient,
            "Writer",
            """
            Turn the supplied research notes into a clear, concise explanation.
            Use only the supplied notes and distinguish uncertainty from established facts.
            """);

        var researchResponse = await researcher.RunAsync(input, cancellationToken: cancellationToken);
        var research = RequireText(researchResponse.Text, "The researcher returned an empty response.");
        writeOutput?.Invoke("\n--- Researcher output ---");
        writeOutput?.Invoke(research);

        var writerResponse = await writer.RunAsync(
            $"Original request: {input}\n\nResearch notes:\n{research}",
            cancellationToken: cancellationToken);
        var result = RequireText(writerResponse.Text, "The writer returned an empty response.");
        writeOutput?.Invoke("\n--- Writer output ---");
        writeOutput?.Invoke(result);
        return result;
    }

    private static async Task<string> RunParallelAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var workers = new[]
        {
            CreateAgent(
                chatClient,
                "WeatherAnalyst",
                """
                Analyze the request from a weather perspective. Do not claim live weather access.
                Clearly label any illustrative or model-generated estimate as unverified.
                """),
            CreateAgent(
                chatClient,
                "NewsAnalyst",
                """
                Analyze the request from a news and current-events perspective.
                No web-search tool is available; do not claim to fetch or verify current news.
                """)
        };
        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            workers,
            AggregateWorkerResponses);

        await using var run = await InProcessExecution.RunAsync(
            workflow,
            new ChatMessage(ChatRole.User, input),
            cancellationToken: cancellationToken);

        List<ChatMessage>? results = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case WorkflowOutputEvent output:
                    results = output.As<List<ChatMessage>>();
                    break;
                case ExecutorFailedEvent failed:
                    var exception = failed.Data as Exception;
                    throw new InvalidOperationException(
                        $"Parallel agent '{failed.ExecutorId}' failed: " +
                        (exception?.Message ?? failed.Data?.ToString() ?? "No failure details were provided."),
                        exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("The parallel multi-agent workflow failed.", error.Exception);
            }
        }

        if (results is null || results.Count < workers.Length ||
            results.Any(message => string.IsNullOrWhiteSpace(message.Text)))
        {
            throw new InvalidOperationException("The parallel workflow did not return every specialist response.");
        }

        var weatherResult = FindWorkerResult(results, "WeatherAnalyst");
        var newsResult = FindWorkerResult(results, "NewsAnalyst");
        writeOutput?.Invoke("\n--- Weather analyst (parallel branch) ---");
        writeOutput?.Invoke(weatherResult.Text);
        writeOutput?.Invoke("\n--- News analyst (parallel branch) ---");
        writeOutput?.Invoke(newsResult.Text);

        var synthesizer = CreateAgent(
            chatClient,
            "Coordinator",
            "Combine the specialist notes into a useful answer. Preserve uncertainty and do not add unsupported facts.");
        var synthesis = await synthesizer.RunAsync(
            $"Request: {input}\n\nWeather analysis:\n{weatherResult.Text}\n\nNews analysis:\n{newsResult.Text}",
            cancellationToken: cancellationToken);
        var final = RequireText(synthesis.Text, "The coordinator returned an empty synthesis.");
        writeOutput?.Invoke("\n--- Coordinated result ---");
        writeOutput?.Invoke(final);
        return final;
    }

    private static async Task<string> RunLoopAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var processor = CreateAgent(
            chatClient,
            "IterativeProcessor",
            """
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
            var result = RequireText(step.Result, "The loop agent returned empty progress.");
            writeOutput?.Invoke($"\n--- Loop iteration {iteration} of {MaxLoopIterations} ---");
            writeOutput?.Invoke(result);

            if (step.IsComplete)
            {
                writeOutput?.Invoke("--- Completion check: complete ---");
                return result;
            }

            if (string.IsNullOrWhiteSpace(step.NextStep))
            {
                throw new InvalidOperationException(
                    "The loop agent marked its work incomplete without providing a next step.");
            }

            progress = $"{result}\nNext step: {step.NextStep}";
            writeOutput?.Invoke("--- Completion check: continue ---");
        }

        throw new InvalidOperationException(
            $"The loop reached its deterministic limit of {MaxLoopIterations} iterations without completion.");
    }

    private static async Task<string> RunCoordinatorAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var greeter = CreateAgent(
            chatClient,
            "Greeter",
            "Greet the user warmly and briefly. Return only the greeting.");
        var taskExecutor = AIFunctionFactory.Create(
            (Func<string, string>)ExecuteTask,
            name: "execute_task",
            description: "Performs a deterministic demonstration task and reports its completion.");
        var coordinator = CreateAgent(
            chatClient,
            "Coordinator",
            """
            Choose exactly one appropriate delegate:
            - use Greeter for a request to greet or say hello
            - use execute_task for a request to perform a task
            Return the delegate's result without inventing extra actions.
            """,
            greeter.AsAIFunction(),
            taskExecutor);

        var response = await coordinator.RunAsync(input, cancellationToken: cancellationToken);
        var result = RequireText(response.Text, "The coordinator returned an empty response.");
        writeOutput?.Invoke("\n--- Coordinator delegation result ---");
        writeOutput?.Invoke(result);
        return result;
    }

    private static async Task<string> RunAgentToolAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var imageTool = AIFunctionFactory.Create(
            (Func<string, string>)GenerateImage,
            name: "generate_image",
            description: "Returns a mock image-generation result for the supplied visual prompt.");
        var imageGenerator = CreateAgent(
            chatClient,
            "ImageGen",
            """
            You are an image-generation specialist. Pass the full visual prompt to generate_image.
            Explain that the returned asset is mock data; do not imply that a real image was generated.
            """,
            imageTool);
        var artist = CreateAgent(
            chatClient,
            "Artist",
            """
            Invent a concise, vivid visual prompt for the user's request.
            Then call the ImageGen specialist with that prompt and report its result.
            """,
            imageGenerator.AsAIFunction());

        var response = await artist.RunAsync(input, cancellationToken: cancellationToken);
        var result = RequireText(response.Text, "The artist agent returned an empty response.");
        writeOutput?.Invoke("\n--- Artist and image specialist result ---");
        writeOutput?.Invoke(result);
        return result;
    }

    private static Task<string> RunCrewAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        return RunCrewAsync(
            async (request, token) =>
                (await CreateCrewSpecialist(chatClient, "researcher")
                    .RunAsync(request, cancellationToken: token)).Text,
            async (request, token) =>
                (await CreateCrewSpecialist(chatClient, "writer")
                    .RunAsync(request, cancellationToken: token)).Text,
            input,
            writeOutput,
            cancellationToken);
    }

    public static AIAgent CreateCrewSpecialist(IChatClient chatClient, string role) => role switch
    {
        "researcher" => CreateAgent(
            chatClient,
            "SeniorResearchAnalyst",
            """
            Identify three important themes relevant to the user's topic and explain their practical impact.
            Use general model knowledge only; do not claim current web research or cite fabricated sources.
            Return concise notes for a writer.
            """),
        "writer" => CreateAgent(
            chatClient,
            "TechnicalContentWriter",
            """
            Write an engaging, accessible article of about 350 words based on the supplied research notes.
            Preserve uncertainty and do not introduce factual claims unsupported by those notes.
            Return the article only.
            """),
        _ => throw new ArgumentException("Select a crew specialist: researcher or writer.", nameof(role))
    };

    public static async Task<string> RunCrewAsync(
        Func<string, CancellationToken, Task<string>> researchTask,
        Func<string, CancellationToken, Task<string>> writingTask,
        string input,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(researchTask);
        ArgumentNullException.ThrowIfNull(writingTask);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        cancellationToken.ThrowIfCancellationRequested();
        var research = RequireText(
            await researchTask(input, cancellationToken),
            "The crew researcher returned an empty response.");
        writeOutput?.Invoke("\n--- Crew research task ---");
        writeOutput?.Invoke(research);

        cancellationToken.ThrowIfCancellationRequested();
        var result = RequireText(await writingTask(
            $"Topic: {input}\n\nResearch task output:\n{research}",
            cancellationToken),
            "The crew writer returned an empty response.");
        writeOutput?.Invoke("\n--- Crew writing task ---");
        writeOutput?.Invoke(result);
        return result;
    }

    private static AIAgent CreateAgent(
        IChatClient chatClient,
        string name,
        string instructions,
        params AITool[] tools) =>
        new ChatClientAgent(
                chatClient,
                instructions,
                name: name,
                tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();

    private static List<ChatMessage> AggregateWorkerResponses(IList<List<ChatMessage>> outputs) =>
        outputs.SelectMany(output => output)
            .Where(message => message.Role == ChatRole.Assistant)
            .ToList();

    private static ChatMessage FindWorkerResult(
        IReadOnlyList<ChatMessage> results,
        string workerName) =>
        results.FirstOrDefault(message =>
            string.Equals(message.AuthorName, workerName, StringComparison.Ordinal) ||
            message.AuthorName?.StartsWith($"{workerName}_", StringComparison.Ordinal) == true)
        ?? throw new InvalidOperationException(
            $"The parallel workflow did not include an attributable response from '{workerName}'.");

    private static string ExecuteTask(string task) =>
        $"Task completed by the deterministic demonstration executor: {task.Trim()}";

    private static string GenerateImage(string prompt) =>
        $"Mock image asset for prompt: {prompt.Trim()} (no image service is connected).";

    private static string RequireText(string? value, string errorMessage) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(errorMessage);

    private sealed record LoopProgress(bool IsComplete, string Result, string NextStep);
}
