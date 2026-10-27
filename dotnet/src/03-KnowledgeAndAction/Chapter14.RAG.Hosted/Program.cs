using AgenticDesignPatterns.Shared;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter14.RAG;
using Chapter14.RAG.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var configuration = builder.Configuration;
var configured = configuration.GetSection("Foundry").Get<FoundrySettings>();
var endpoint = configuration["Foundry:ProjectEndpoint"] ?? configuration["FOUNDRY_PROJECT_ENDPOINT"];
var modelDeployment = configuration["Foundry:ModelDeployment"] ?? configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"];

if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(modelDeployment))
{
    throw new InvalidOperationException(
        "Configure Foundry:ProjectEndpoint and Foundry:ModelDeployment in shared user-secrets " +
        "or Foundry__ProjectEndpoint and Foundry__ModelDeployment environment variables.");
}

var settings = new FoundrySettings
{
    ProjectEndpoint = endpoint,
    ModelDeployment = modelDeployment,
    ApiKey = configured?.ApiKey ?? string.Empty,
    ModelDeployments = configured?.ModelDeployments ?? []
};
var deploymentAlias = configuration["deployment"];
var deployment = settings.ResolveModelDeployment(deploymentAlias);
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
});

IChatClient chatClient = string.IsNullOrWhiteSpace(settings.ApiKey)
    ? new AzureOpenAIClient(
            new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)),
            credential)
        .GetChatClient(deployment)
        .AsIChatClient()
    : FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);

using IChatClient modelClient = chatClient;
builder.ConfigureTracing(tracing => tracing.AddSource(RagAgent.TelemetrySourceName));
builder.Services.AddFoundryResponses(new RagHostedAgent(modelClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
