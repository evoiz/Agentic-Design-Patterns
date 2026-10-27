using System.Text.Json;
using Chapter16.ResourceOptimization.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter16.ResourceOptimization;

public enum RoutingStrategy
{
    Complexity,
    IntentClassification
}

public static class ResourceOptimizationWorkflow
{
    public const string TelemetrySourceName = "Chapter16.ResourceOptimization";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static int CountWords(string request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public static OptimizationCategory RouteByComplexity(string request) =>
        CountWords(request) < 20
            ? OptimizationCategory.Simple
            : OptimizationCategory.Reasoning;

    public static string ResolveDeploymentAlias(
        IReadOnlyDictionary<string, string> deployments,
        string alias,
        string fallbackDeployment)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackDeployment);

        var deployment = deployments.FirstOrDefault(
            entry => string.Equals(entry.Key, alias, StringComparison.OrdinalIgnoreCase)).Value;
        return string.IsNullOrWhiteSpace(deployment) ? fallbackDeployment : deployment;
    }

    public static OptimizationCategory ParseClassification(string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        ClassificationPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ClassificationPayload>(output, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The classifier must return JSON with a 'classification' field.",
                exception);
        }

        return payload?.Classification?.Trim().ToLowerInvariant() switch
        {
            "simple" => OptimizationCategory.Simple,
            "reasoning" => OptimizationCategory.Reasoning,
            "internet_search" => OptimizationCategory.InternetSearch,
            _ => throw new InvalidOperationException(
                "The classifier returned an unsupported category. Expected simple, reasoning, or internet_search.")
        };
    }

    public static async Task<OptimizationResult> RunAsync(
        string request,
        RoutingStrategy strategy,
        IChatClient classifierClient,
        IReadOnlyDictionary<OptimizationCategory, OptimizationModelClient> modelClients,
        IGoogleCustomSearch? searchClient = null,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default,
        AIAgent? webSearchAgent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        ArgumentNullException.ThrowIfNull(classifierClient);
        ArgumentNullException.ThrowIfNull(modelClients);

        var workflow = Create(
            strategy,
            classifierClient,
            modelClients,
            searchClient,
            writeOutput,
            webSearchAgent);
        await using var run = await InProcessExecution.RunAsync(
            workflow,
            new ChatMessage(ChatRole.User, request),
            cancellationToken: cancellationToken);

        OptimizationResult? result = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            if (workflowEvent is WorkflowOutputEvent outputEvent)
            {
                result = outputEvent.As<OptimizationResult>();
            }
            else if (workflowEvent is ExecutorFailedEvent failedEvent)
            {
                throw new InvalidOperationException(
                    $"Workflow executor '{failedEvent.ExecutorId}' failed.",
                    failedEvent.Data as Exception);
            }
            else if (workflowEvent is WorkflowErrorEvent errorEvent)
            {
                throw new InvalidOperationException(
                    "The resource optimization workflow failed.",
                    errorEvent.Exception);
            }
        }

        return result
            ?? throw new InvalidOperationException(
                "The resource optimization workflow completed without a result.");
    }

    private static Workflow Create(
        RoutingStrategy strategy,
        IChatClient classifierClient,
        IReadOnlyDictionary<OptimizationCategory, OptimizationModelClient> modelClients,
        IGoogleCustomSearch? searchClient,
        Action<string>? writeOutput,
        AIAgent? webSearchAgent)
    {
        foreach (var category in Enum.GetValues<OptimizationCategory>())
        {
            if (!modelClients.ContainsKey(category))
            {
                throw new ArgumentException(
                    $"A model client is required for the '{category}' route.",
                    nameof(modelClients));
            }
        }

        var classifier = strategy == RoutingStrategy.IntentClassification
            ? CreateAgent(
                classifierClient,
                "ResourceClassifier",
                """
                Classify the user request using exactly one category:
                - simple: a direct factual question that needs no reasoning or current information
                - reasoning: logic, math, or multi-step inference
                - internet_search: current events, recent data, or information that changes over time

                Return only JSON in this shape: {"classification":"simple"}
                Replace the example value with exactly simple, reasoning, or internet_search.
                """)
            : null;
        var classifierUsage = classifier?.UsageClient;
        var router = new RouteExecutor(strategy, classifier, writeOutput);
        var simple = CreateAnswerExecutor(
            OptimizationCategory.Simple,
            modelClients[OptimizationCategory.Simple],
            "Answer direct factual questions concisely. If uncertain, state the uncertainty.",
            searchClient,
            writeOutput);
        var reasoning = CreateAnswerExecutor(
            OptimizationCategory.Reasoning,
            modelClients[OptimizationCategory.Reasoning],
            "Solve the user's multi-step or analytical request. Give a concise answer and a brief explanation; do not reveal private chain-of-thought.",
            searchClient,
            writeOutput);
        var internetSearch = CreateAnswerExecutor(
            OptimizationCategory.InternetSearch,
            modelClients[OptimizationCategory.InternetSearch],
            "Answer using the supplied web results. Cite relevant result links and distinguish sourced facts from uncertainty.",
            searchClient,
            writeOutput,
            webSearchAgent);
        router.SetClassifierUsage(classifierUsage);

        return new WorkflowBuilder(router)
            .AddEdge<RoutedRequest>(router, simple, route => route is { Category: OptimizationCategory.Simple })
            .AddEdge<RoutedRequest>(router, reasoning, route => route is { Category: OptimizationCategory.Reasoning })
            .AddEdge<RoutedRequest>(router, internetSearch, route => route is { Category: OptimizationCategory.InternetSearch })
            .WithOutputFrom(simple, reasoning, internetSearch)
            .Build();
    }

    private static AnswerExecutor CreateAnswerExecutor(
        OptimizationCategory category,
        OptimizationModelClient modelClient,
        string instructions,
        IGoogleCustomSearch? searchClient,
        Action<string>? writeOutput,
        AIAgent? webSearchAgent = null)
    {
        var agent = CreateAgent(
            modelClient.Client,
            category switch
            {
                OptimizationCategory.Simple => "SimpleAnswerer",
                OptimizationCategory.Reasoning => "ReasoningAnswerer",
                OptimizationCategory.InternetSearch => "SearchAnswerer",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            },
            instructions);
        return new AnswerExecutor(category, modelClient.Deployment, agent, searchClient, writeOutput, webSearchAgent);
    }

    private static TrackedAgent CreateAgent(IChatClient chatClient, string name, string instructions)
    {
        var usageClient = new UsageTrackingChatClient(chatClient);
        var agent = new ChatClientAgent(
                usageClient,
                instructions,
                name: name,
                description: $"Chapter 16 {name} agent.")
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
        return new TrackedAgent(agent, usageClient);
    }

    private static UsageDetails? CombineUsage(UsageDetails? first, UsageDetails? second)
    {
        if (first is null)
        {
            return second;
        }

        var combined = new UsageDetails();
        combined.Add(first);
        if (second is not null)
        {
            combined.Add(second);
        }

        return combined;
    }

    private static string BuildSearchPrompt(string request, IReadOnlyList<GoogleSearchResult> results)
    {
        var searchContext = results.Count == 0
            ? "No web results were found."
            : string.Join(
                "\n\n",
                results.Select(result =>
                    $"Title: {result.Title}\nSnippet: {result.Snippet}\nLink: {result.Link}"));

        return $"Use the following web results to answer the user query. Do not invent sources.\n\n" +
            $"{searchContext}\n\nQuery: {request}";
    }

    private sealed record ClassificationPayload(string? Classification);
    private sealed record TrackedAgent(AIAgent Agent, UsageTrackingChatClient UsageClient);
    private sealed record RoutedRequest(
        string Prompt,
        OptimizationCategory Category,
        int WordCount,
        UsageDetails? ClassifierUsage);

    private sealed class UsageTrackingChatClient(IChatClient innerClient)
        : DelegatingChatClient(innerClient)
    {
        public UsageDetails? Usage { get; private set; }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await InnerClient.GetResponseAsync(messages, options, cancellationToken);
            if (response.Usage is not null)
            {
                Usage ??= new UsageDetails();
                Usage.Add(response.Usage);
            }

            return response;
        }
    }

    private sealed class RouteExecutor(
        RoutingStrategy strategy,
        TrackedAgent? classifier,
        Action<string>? writeOutput)
        : Executor<ChatMessage, RoutedRequest>("ResourceRouter")
    {
        private UsageTrackingChatClient? _classifierUsage;

        public void SetClassifierUsage(UsageTrackingChatClient? usageClient) =>
            _classifierUsage = usageClient;

        public override async ValueTask<RoutedRequest> HandleAsync(
            ChatMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var prompt = message.Text;
            ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
            var wordCount = CountWords(prompt);

            OptimizationCategory category;
            if (strategy == RoutingStrategy.Complexity)
            {
                category = RouteByComplexity(prompt);
                writeOutput?.Invoke("\n--- Complexity router decision ---");
                writeOutput?.Invoke($"Word count: {wordCount} (fewer than 20 uses the simple route)");
            }
            else
            {
                if (classifier is null)
                {
                    throw new InvalidOperationException("The intent classifier was not configured.");
                }

                var response = await classifier.Agent.RunAsync(
                    prompt,
                    cancellationToken: cancellationToken);
                category = ParseClassification(response.Text);
                writeOutput?.Invoke("\n--- Intent classifier decision ---");
                writeOutput?.Invoke($"Classification: {category}");
            }

            writeOutput?.Invoke($"Route: {category}");
            return new RoutedRequest(prompt, category, wordCount, _classifierUsage?.Usage);
        }
    }

    private sealed class AnswerExecutor(
        OptimizationCategory category,
        string deployment,
        TrackedAgent answerer,
        IGoogleCustomSearch? searchClient,
        Action<string>? writeOutput,
        AIAgent? webSearchAgent)
        : Executor<RoutedRequest, OptimizationResult>($"{category}Answerer")
    {
        public override async ValueTask<OptimizationResult> HandleAsync(
            RoutedRequest route,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<GoogleSearchResult> searchResults = [];
            var answerInput = route.Prompt;
            if (category == OptimizationCategory.InternetSearch && webSearchAgent is null)
            {
                if (searchClient is null)
                {
                    throw new InvalidOperationException(
                        "This request needs current information, but Google Custom Search is not configured. " +
                        "Configure both GoogleCustomSearch:ApiKey and GoogleCustomSearch:SearchEngineId.");
                }

                searchResults = await searchClient.SearchAsync(route.Prompt, cancellationToken);
                writeOutput?.Invoke("\n--- Google Custom Search results ---");
                foreach (var result in searchResults)
                {
                    writeOutput?.Invoke($"{result.Title}: {result.Snippet} ({result.Link})");
                }

                answerInput = BuildSearchPrompt(route.Prompt, searchResults);
            }

            var response = await (webSearchAgent ?? answerer.Agent).RunAsync(
                answerInput,
                cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException($"The {category} model returned an empty response.");
            }

            var answer = response.Text;
            if (webSearchAgent is not null)
            {
                var citations = response.Messages
                    .SelectMany(message => message.Contents)
                    .SelectMany(content => content.Annotations ?? [])
                    .OfType<CitationAnnotation>()
                    .Where(citation => citation.Url is not null)
                    .DistinctBy(citation => citation.Url)
                    .Select(citation => $"{citation.Title ?? "Source"}: {citation.Url}")
                    .ToList();
                if (citations.Count > 0)
                {
                    answer += "\n\nSources:\n" + string.Join("\n", citations);
                }
            }

            var usage = CombineUsage(
                route.ClassifierUsage,
                webSearchAgent is null ? answerer.UsageClient.Usage : response.Usage);
            writeOutput?.Invoke($"\n--- {category} answer ({deployment}) ---");
            writeOutput?.Invoke(answer);
            writeOutput?.Invoke(
                $"Token usage: input={usage?.InputTokenCount?.ToString() ?? "not reported"}, " +
                $"output={usage?.OutputTokenCount?.ToString() ?? "not reported"}, " +
                $"total={usage?.TotalTokenCount?.ToString() ?? "not reported"}");

            return new OptimizationResult(
                category,
                deployment,
                answer,
                route.WordCount,
                usage?.InputTokenCount,
                usage?.OutputTokenCount,
                usage?.TotalTokenCount,
                searchResults);
        }
    }
}
