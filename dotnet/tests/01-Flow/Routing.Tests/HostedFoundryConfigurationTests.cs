using AgenticDesignPatterns.Shared.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenticDesignPatterns.Routing.Tests;

public sealed class HostedFoundryConfigurationTests
{
    [Fact]
    public void Nested_secrets_support_empty_key_and_named_deployments()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            {
              "Foundry": {
                "ProjectEndpoint": "https://demo.services.ai.azure.com/api/projects/sample",
                "ApiKey": "",
                "ModelDeployment": "gpt-5.4-mini",
                "ModelDeployments": {
                  "fast": "gpt-5.4-mini",
                  "reasoning": "gpt-6-astra",
                  "alternate": "gpt-6-luna",
                  "router": "model-router"
                }
              }
            }
            """));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();

        var settings = HostedFoundryConfiguration.LoadSettings(configuration);

        Assert.Equal(string.Empty, settings.ApiKey);
        Assert.Equal("gpt-5.4-mini", settings.ResolveModelDeployment());
        Assert.Equal("gpt-5.4-mini", settings.ResolveModelDeployment("fast"));
        Assert.Equal("gpt-6-astra", settings.ResolveModelDeployment("reasoning"));
        Assert.Equal("gpt-6-luna", settings.ResolveModelDeployment("alternate"));
        Assert.Equal("model-router", settings.ResolveModelDeployment("router"));
    }

    [Fact]
    public void Platform_variables_remain_supported_without_user_secrets()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["FOUNDRY_PROJECT_ENDPOINT"] = "https://demo.services.ai.azure.com/api/projects/sample",
                ["AZURE_AI_MODEL_DEPLOYMENT_NAME"] = "gpt-5-mini"
            }).Build();

        var settings = HostedFoundryConfiguration.LoadSettings(configuration);

        Assert.Equal("gpt-5-mini", settings.ModelDeployment);
        Assert.Equal(string.Empty, settings.ApiKey);
    }

    [Fact]
    public void Canonical_configuration_overrides_platform_fallback()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Foundry:ProjectEndpoint"] = "https://demo.services.ai.azure.com/api/projects/sample",
                ["Foundry:ModelDeployment"] = "gpt-5.4-mini",
                ["Foundry:ApiKey"] = "test-key",
                ["AZURE_AI_MODEL_DEPLOYMENT_NAME"] = "gpt-5-mini"
            }).Build();

        var settings = HostedFoundryConfiguration.LoadSettings(configuration);

        Assert.Equal("gpt-5.4-mini", settings.ModelDeployment);
        Assert.Equal("test-key", settings.ApiKey);
        Assert.Throws<InvalidOperationException>(() => settings.ResolveModelDeployment("missing"));
    }

    [Fact]
    public void Missing_configuration_reports_canonical_setting_names()
    {
        var configuration = new ConfigurationBuilder().Build();
        var error = Assert.Throws<InvalidOperationException>(() =>
            HostedFoundryConfiguration.LoadSettings(configuration));
        Assert.Contains("Foundry__ProjectEndpoint", error.Message);
        Assert.Contains("Foundry__ModelDeployment", error.Message);
    }
}
