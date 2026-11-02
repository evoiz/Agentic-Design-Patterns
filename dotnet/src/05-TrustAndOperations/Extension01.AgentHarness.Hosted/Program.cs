using AgenticDesignPatterns.Shared;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Extension01.AgentHarness;
using Extension01.AgentHarness.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var configuration = builder.Configuration;
var endpoint = configuration["Foundry:ProjectEndpoint"] ?? configuration["FOUNDRY_PROJECT_ENDPOINT"];
var deployment = configuration["Foundry:ModelDeployment"] ?? configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"];
if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
{
    throw new InvalidOperationException(
        "Configure Foundry__ProjectEndpoint and Foundry__ModelDeployment, or " +
        "FOUNDRY_PROJECT_ENDPOINT and AZURE_AI_MODEL_DEPLOYMENT_NAME. Hosted inference uses Entra ID, never an API key.");
}

TokenCredential credential = !string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT"))
    ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
    : new DefaultAzureCredential();
using IChatClient model = new AzureOpenAIClient(
        new Uri(FoundrySettings.ResolveOpenAIEndpoint(endpoint).GetLeftPart(UriPartial.Authority)), credential)
    .GetChatClient(deployment)
    .AsIChatClient();
builder.ConfigureTracing(tracing => tracing.AddSource(HarnessRunner.TelemetrySourceName));
#pragma warning disable MAAI001 // Explicit request-scoped isolation is an experimental hosting API.
builder.Services.AddSingleton<HostedSessionIsolationKeyProvider, RequestIsolationKeyProvider>();
#pragma warning restore MAAI001
builder.Services.AddFoundryResponses(new HarnessHostedAgent(
    model, configuration.GetValue<bool>("Harness:EnableSensitiveTelemetry")));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
builder.Build().Run();
