using AgenticDesignPatterns.Shared;
using Microsoft.Extensions.Configuration;

namespace Chapter10.MCP.Hosted;

public static class HostedFoundryConfiguration
{
    public static FoundrySettings LoadSettings(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Foundry").Get<FoundrySettings>();
        var endpoint = configuration["Foundry:ProjectEndpoint"]
            ?? configuration["FOUNDRY_PROJECT_ENDPOINT"];
        var deployment = configuration["Foundry:ModelDeployment"]
            ?? configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ProjectEndpoint and Foundry:ModelDeployment in shared user-secrets " +
                "or Foundry__ProjectEndpoint and Foundry__ModelDeployment environment variables.");
        }

        return new FoundrySettings
        {
            ProjectEndpoint = endpoint,
            ModelDeployment = deployment,
            ApiKey = configured?.ApiKey ?? string.Empty,
            ModelDeployments = configured?.ModelDeployments ?? []
        };
    }
}
