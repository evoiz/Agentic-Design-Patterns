using System.Text.Json;
using Chapter16.ResourceOptimization.Models;
using Microsoft.Agents.AI;
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
    private static readonly JsonSerializerOptions ClassifierJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new OptimizationCategoryConverter() }
    };

    public static int CountWords(string request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public static OptimizationCategory RouteByComplexity(string request) =>
        CountWords(request) < 20 ? OptimizationCategory.Simple : OptimizationCategory.Reasoning;

    public static string ResolveDeploymentAlias(
        IReadOnlyDictionary<string, string> deployments, string alias, string fallbackDeployment)
    {
        ArgumentNullException.ThrowIfNull(deployments);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackDeployment);
        var deployment = deployments.FirstOrDefault(
            entry => string.Equals(entry.Key, alias, StringComparison.OrdinalIgnoreCase)).Value;
        return string.IsNullOrWhiteSpace(deployment) ? fallbackDeployment : deployment;
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
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var category in Enum.GetValues<OptimizationCategory>())
        {
            if (!modelClients.ContainsKey(category))
                throw new ArgumentException($"A model client is required for the '{category}' route.", nameof(modelClients));
        }

        var wordCount = CountWords(request);
        var usage = new UsageDetails();
        var usageReported = false;
        void RecordUsage(UsageDetails? reported)
        {
            if (reported is null) return;
            usageReported = true;
            usage.Add(reported);
        }

        OptimizationCategory route;
        switch (strategy)
        {
            case RoutingStrategy.Complexity:
                route = RouteByComplexity(request);
                writeOutput?.Invoke("\n--- Complexity router decision ---");
                writeOutput?.Invoke($"Word count: {wordCount} (fewer than 20 uses the simple route)");
                break;
            case RoutingStrategy.IntentClassification:
                var classifier = CreateAgent(classifierClient, "ResourceClassifier", """
                    Classify the user request using exactly one category:
                    - simple: a direct factual question that needs no reasoning or current information
                    - reasoning: logic, math, or multi-step inference
                    - internet_search: current events, recent data, or information that changes over time
                    Return the required structured classification.
                    """);
                var classification = await classifier.RunAsync<ClassificationPayload>(
                    request, serializerOptions: ClassifierJson, cancellationToken: cancellationToken);
                route = classification.Result?.Classification
                    ?? throw new InvalidOperationException("The classifier returned no category.");
                if (!Enum.IsDefined(route))
                    throw new InvalidOperationException("The classifier returned an unsupported category.");
                RecordUsage(classification.Usage);
                writeOutput?.Invoke("\n--- Intent classifier decision ---");
                writeOutput?.Invoke($"Classification: {route}");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy));
        }
        writeOutput?.Invoke($"Route: {route}");

        IReadOnlyList<GoogleSearchResult> searchResults = [];
        var answerInput = request;
        if (route == OptimizationCategory.InternetSearch && webSearchAgent is null)
        {
            if (searchClient is null)
                throw new InvalidOperationException(
                    "This request needs current information, but Google Custom Search is not configured. " +
                    "Configure both GoogleCustomSearch:ApiKey and GoogleCustomSearch:SearchEngineId.");
            searchResults = await searchClient.SearchAsync(request, cancellationToken);
            writeOutput?.Invoke("\n--- Google Custom Search results ---");
            foreach (var result in searchResults)
                writeOutput?.Invoke($"{result.Title}: {result.Snippet} ({result.Link})");
            var context = searchResults.Count == 0 ? "No web results were found."
                : string.Join("\n\n", searchResults.Select(result =>
                    $"Title: {result.Title}\nSnippet: {result.Snippet}\nLink: {result.Link}"));
            answerInput = $"Use the following web results to answer the user query. Do not invent sources.\n\n{context}\n\nQuery: {request}";
        }

        var selected = modelClients[route];
        var answerer = route == OptimizationCategory.InternetSearch && webSearchAgent is not null
            ? webSearchAgent
            : CreateAgent(selected.Client, $"{route}Answerer", route switch
            {
                OptimizationCategory.Simple => "Answer direct factual questions concisely. If uncertain, state the uncertainty.",
                OptimizationCategory.Reasoning => "Solve the user's multi-step or analytical request. Give a concise answer and a brief explanation; do not reveal private chain-of-thought.",
                _ => "Answer using the supplied web results. Cite relevant result links and distinguish sourced facts from uncertainty."
            });
        var response = await answerer.RunAsync(answerInput, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(response.Text))
            throw new InvalidOperationException($"The {route} model returned an empty response.");
        RecordUsage(response.Usage);
        var answer = response.Text;
        if (route == OptimizationCategory.InternetSearch && webSearchAgent is not null)
        {
            var citations = response.Messages.SelectMany(message => message.Contents)
                .SelectMany(content => content.Annotations ?? [])
                .OfType<CitationAnnotation>().Where(citation => citation.Url is not null)
                .DistinctBy(citation => citation.Url)
                .Select(citation => $"{citation.Title ?? "Source"}: {citation.Url}").ToArray();
            if (citations.Length > 0) answer += "\n\nSources:\n" + string.Join("\n", citations);
        }
        writeOutput?.Invoke($"\n--- {route} answer ({selected.Deployment}) ---");
        writeOutput?.Invoke(answer);
        writeOutput?.Invoke(
            $"Token usage: input={usage.InputTokenCount?.ToString() ?? "not reported"}, " +
            $"output={usage.OutputTokenCount?.ToString() ?? "not reported"}, " +
            $"total={usage.TotalTokenCount?.ToString() ?? "not reported"}");
        return new OptimizationResult(route, selected.Deployment, answer, wordCount,
            usageReported ? usage.InputTokenCount : null,
            usageReported ? usage.OutputTokenCount : null,
            usageReported ? usage.TotalTokenCount : null, searchResults);
    }

    private static AIAgent CreateAgent(IChatClient client, string name, string instructions) =>
        new ChatClientAgent(client, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();

    public sealed record ClassificationPayload(OptimizationCategory? Classification);
}
