using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace AgenticDesignPatterns.Shared.Hosting;

public static class HostedFoundryConfiguration
{
    public static FoundrySettings LoadSettings(IConfiguration configuration)
    {
        var settings = configuration.GetSection("Foundry").Get<FoundrySettings>();
        var endpoint = configuration["Foundry:ProjectEndpoint"] ?? configuration["FOUNDRY_PROJECT_ENDPOINT"];
        var deployment = configuration["Foundry:ModelDeployment"] ?? configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ProjectEndpoint and Foundry:ModelDeployment in shared user-secrets " +
                "or Foundry__ProjectEndpoint and Foundry__ModelDeployment environment variables. " +
                "Foundry runtime variables FOUNDRY_PROJECT_ENDPOINT and AZURE_AI_MODEL_DEPLOYMENT_NAME are also supported.");
        }

        return new FoundrySettings
        {
            ProjectEndpoint = endpoint,
            ModelDeployment = deployment,
            ApiKey = settings?.ApiKey ?? string.Empty,
            ModelDeployments = settings?.ModelDeployments ?? []
        };
    }

    public static IChatClient CreateChatClient(IConfiguration configuration, string? deploymentAlias = null)
    {
        var settings = LoadSettings(configuration);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        }

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
        });
        return new AzureOpenAIClient(
                new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)), credential)
            .GetChatClient(settings.ResolveModelDeployment(deploymentAlias))
            .AsIChatClient();
    }
}
