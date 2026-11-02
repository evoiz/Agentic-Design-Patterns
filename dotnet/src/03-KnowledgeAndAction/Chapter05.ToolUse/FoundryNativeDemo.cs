using Azure.Identity;
using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace Chapter05.ToolUse;

public static class FoundryNativeDemo
{
    public static async Task RunAsync(
        string toolName,
        string? vectorStoreId = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
            .AddEnvironmentVariables()
            .Build();
        var settings = HostedFoundryConfiguration.LoadSettings(configuration);
        vectorStoreId ??= configuration["Foundry:FileSearchVectorStoreId"];

        var (instructions, query, tool) = toolName.ToLowerInvariant() switch
        {
            "web" => (
                "Answer using current web sources and clearly distinguish sourced findings from your own synthesis.",
                "What are the latest AI news developments? Summarize briefly and cite sources.",
                FoundryAITool.CreateWebSearchTool()),
            "code" => (
                "Use the hosted sandboxed code interpreter to calculate the requested result, then report the result.",
                "Calculate the value of (5 + 7) * 3 and 10 factorial using code.",
                // The pinned OpenAI Responses types are currently marked as experimental.
#pragma warning disable OPENAI001
                FoundryAITool.CreateCodeInterpreterTool(
                    new CodeInterpreterToolContainer(
                        CodeInterpreterToolContainerConfiguration.CreateAutomaticContainerConfiguration([])))),
#pragma warning restore OPENAI001
            "files" when !string.IsNullOrWhiteSpace(vectorStoreId) => (
                "Answer only from the connected file-search index. State when the documents do not contain the answer.",
                "Summarize the main points about the Q2 strategy document.",
                FoundryAITool.CreateFileSearchTool([vectorStoreId])),
            "files" => throw new InvalidOperationException(
                "File Search requires an existing vector store. Set Foundry:FileSearchVectorStoreId or pass a vector store ID."),
            _ => throw new ArgumentException("Select one Foundry-native tool: web, code, or files.", nameof(toolName))
        };

        var projectClient = new AIProjectClient(
            new Uri(settings.ProjectEndpoint),
            new DefaultAzureCredential());
        AIAgent agent = projectClient.AsAIAgent(
            model: settings.ModelDeployment,
            instructions: instructions,
            name: $"Chapter05{toolName}Agent",
            tools: [tool]);

        Console.WriteLine($"--- Foundry-native {toolName} tool ---");
        Console.WriteLine($"Model deployment: {settings.ModelDeployment}");
        Console.WriteLine($"User: {query}");
        var response = await agent.RunAsync(query, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException($"The Foundry-native {toolName} agent returned an empty response.");
        }

        Console.WriteLine("\n--- Final response ---");
        Console.WriteLine(response.Text);
    }

}
