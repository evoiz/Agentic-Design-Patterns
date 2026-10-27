using AgenticDesignPatterns.Shared;
using Azure.Core;
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Configuration;

namespace DecisionModels;

public static class DecisionConfiguration
{
    public static FoundrySettings LoadSettings(IConfiguration configuration, bool hosted = false)
    {
        var settings = new FoundrySettings
        {
            ProjectEndpoint = configuration["Foundry:ProjectEndpoint"]
                ?? configuration["FOUNDRY_PROJECT_ENDPOINT"] ?? "",
            ApiKey = hosted ? "" : configuration["Foundry:ApiKey"] ?? "",
            ModelDeployment = configuration["Foundry:ModelDeployment"]
                ?? configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"] ?? "",
            ModelDeployments = configuration.GetSection("Foundry:ModelDeployments")
                .Get<Dictionary<string, string>>() ?? []
        };
        _ = settings.OpenAIEndpoint;
        if (!hosted && string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ApiKey in Shared user-secrets or Foundry__ApiKey for console runs.");
        }

        return settings;
    }

    public static FoundryDecisionClient CreateClient(
        FoundrySettings settings, string provider, TokenCredential? credential = null)
    {
        var (api, alias) = provider switch
        {
            "microsoft" => (FoundryDecisionApi.SystemOne, "decision"),
            "openai" => (FoundryDecisionApi.OpenAIDecisions, "openaiDecision"),
            _ => throw new ArgumentException("Provider must be microsoft or openai.", nameof(provider))
        };
        var resourceRoot = new Uri(new Uri(settings.ProjectEndpoint).GetLeftPart(UriPartial.Authority));
        return new FoundryDecisionClient(new FoundryDecisionOptions
        {
            Api = api,
            Endpoint = api == FoundryDecisionApi.SystemOne ? resourceRoot : settings.OpenAIEndpoint,
            Model = settings.ResolveModelDeployment(alias),
            ApiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey,
            Credential = credential,
            Timeout = TimeSpan.FromSeconds(90)
        });
    }
}
