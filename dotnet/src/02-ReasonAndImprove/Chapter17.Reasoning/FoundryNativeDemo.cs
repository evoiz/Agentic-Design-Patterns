using AgenticDesignPatterns.Shared;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace Chapter17.Reasoning;

public static class FoundryNativeDemo
{
    public static async Task RunAsync(
        string toolName,
        string? deploymentSelection = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
            .AddEnvironmentVariables()
            .Build();
        var projectEndpoint = configuration["Foundry:ProjectEndpoint"]
            ?? configuration["FOUNDRY_PROJECT_ENDPOINT"];
        var defaultDeployment = configuration["Foundry:ModelDeployment"]
            ?? configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"];
        if (string.IsNullOrWhiteSpace(projectEndpoint) || string.IsNullOrWhiteSpace(defaultDeployment))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ProjectEndpoint and Foundry:ModelDeployment in shared user-secrets " +
                "or Foundry__ProjectEndpoint and Foundry__ModelDeployment environment variables.");
        }

        var deployments = configuration.GetSection("Foundry:ModelDeployments")
            .Get<Dictionary<string, string>>() ?? [];
        var deployment = FoundrySettings.ResolveModelDeployment(
            deploymentSelection,
            defaultDeployment,
            deployments);

        var (instructions, input, tool) = toolName.ToLowerInvariant() switch
        {
            "web-search" => (
                "Use current web sources to answer the question. Separate source-backed findings from synthesis.",
                "Explain current research directions in quantum computing and cite the sources returned by web search.",
                FoundryAITool.CreateWebSearchTool()),
            "code-interpreter" => (
                "Use the sandboxed code interpreter to calculate the requested values, then explain the results.",
                "Calculate 10 factorial and (5 + 7) * 3 using code.",
#pragma warning disable OPENAI001
                FoundryAITool.CreateCodeInterpreterTool(
                    new CodeInterpreterToolContainer(
                        CodeInterpreterToolContainerConfiguration.CreateAutomaticContainerConfiguration([])))
#pragma warning restore OPENAI001
            ),
            _ => throw new ArgumentException(
                "Select one Foundry-native tool: web-search or code-interpreter.",
                nameof(toolName))
        };

        var projectClient = new AIProjectClient(new Uri(projectEndpoint), new DefaultAzureCredential());
        AIAgent agent = projectClient.AsAIAgent(
            model: deployment,
            instructions: instructions,
            name: $"Chapter17{toolName.Replace("-", string.Empty, StringComparison.Ordinal)}Agent",
            tools: [tool]);

        Console.WriteLine($"--- Foundry-native {toolName} ---");
        Console.WriteLine($"Model deployment: {deployment}");
        var response = await agent.RunAsync(input, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException($"The Foundry-native {toolName} agent returned an empty response.");
        }

        Console.WriteLine("\n--- Final response ---");
        Console.WriteLine(response.Text);
    }
}
