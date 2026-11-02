using AgenticDesignPatterns.Shared;
using Azure.AI.AgentServer.Core;
using Azure.AI.OpenAI;
using Azure.Identity;
using Extension02.DecisionModels;
using Extension02.DecisionModels.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var configuration = builder.Configuration;
var settings = DecisionConfiguration.LoadSettings(configuration, hosted: true);
if (string.IsNullOrWhiteSpace(settings.ModelDeployment))
{
    throw new InvalidOperationException("Configure Foundry:ModelDeployment with a generative chat deployment.");
}
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = configuration["AZURE_TENANT_ID"]
});
using var decisions = DecisionConfiguration.CreateClient(
    settings, configuration["Decisions:Provider"] ?? "microsoft", credential);
using IChatClient chat = new AzureOpenAIClient(
    new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)), credential)
    .GetChatClient(settings.ModelDeployment).AsIChatClient();

builder.ConfigureTracing(tracing => tracing.AddSource(DecisionWorkflow.TelemetrySourceName));
builder.Services.AddFoundryResponses(new DecisionSupportAgent(decisions, chat));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
