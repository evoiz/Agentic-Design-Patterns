using AgenticDesignPatterns.Shared;
using Azure.Identity;
using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;

namespace Chapter14.RAG;

public static class FoundryNativeDemo
{
    public static async Task<string> RunAsync(
        string mode,
        string question,
        string? deploymentAlias = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
            .AddEnvironmentVariables()
            .Build();
        var projectEndpoint = configuration["Foundry:ProjectEndpoint"]
            ?? configuration["FOUNDRY_PROJECT_ENDPOINT"];
        var configuredDeployment = configuration["Foundry:ModelDeployment"]
            ?? configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"];
        if (string.IsNullOrWhiteSpace(projectEndpoint) ||
            string.IsNullOrWhiteSpace(configuredDeployment))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ProjectEndpoint and Foundry:ModelDeployment in shared user-secrets " +
                "or Foundry__ProjectEndpoint and Foundry__ModelDeployment environment variables.");
        }

        var deployments = configuration.GetSection("Foundry:ModelDeployments")
            .Get<Dictionary<string, string>>() ?? [];
        var deployment = FoundrySettings.ResolveModelDeployment(
            deploymentAlias,
            configuredDeployment,
            deployments);

        var (instructions, tool) = mode.ToLowerInvariant() switch
        {
            "foundry-web" => (
                "Use web search to answer the caller's question with current sources. Distinguish sourced facts from synthesis.",
                FoundryAITool.CreateWebSearchTool()),
            "foundry-files" => CreateFileSearchTool(configuration),
            _ => throw new ArgumentException(
                "Select the Foundry-native variant 'foundry-web' or 'foundry-files'.",
                nameof(mode))
        };

        var projectClient = new AIProjectClient(new Uri(projectEndpoint), new DefaultAzureCredential());
        AIAgent agent = projectClient.AsAIAgent(
            model: deployment,
            instructions: instructions,
            name: mode.Equals("foundry-web", StringComparison.OrdinalIgnoreCase)
                ? "Chapter14WebSearchAgent"
                : "Chapter14FileSearchAgent",
            tools: [tool]);

        var response = await agent.RunAsync(question, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException($"The {mode} agent returned an empty response.");
        }

        return FormatResponse(response);
    }

    public static string FormatResponse(AgentResponse response)
    {
        var citations = response.Messages
            .SelectMany(message => message.Contents)
            .SelectMany(content => content.Annotations ?? [])
            .OfType<CitationAnnotation>()
            .Where(citation => citation.Url is not null)
            .DistinctBy(citation => citation.Url)
            .Select(citation => $"{citation.Title ?? "Source"}: {citation.Url}")
            .ToList();

        return citations.Count == 0
            ? response.Text
            : response.Text + "\n\nSources:\n" + string.Join("\n", citations);
    }

    private static (string Instructions, AITool Tool) CreateFileSearchTool(IConfiguration configuration)
    {
        var vectorStoreId = configuration["Foundry:FileSearchVectorStoreId"];
        if (string.IsNullOrWhiteSpace(vectorStoreId))
        {
            throw new InvalidOperationException(
                "File Search requires a Foundry vector store. Set Foundry:FileSearchVectorStoreId in shared user-secrets.");
        }

        return (
            "Answer only from the connected file-search index. Cite the retrieved documents and say when they do not contain the answer.",
            FoundryAITool.CreateFileSearchTool([vectorStoreId]));
    }
}
