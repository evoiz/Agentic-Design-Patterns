using Azure.Identity;
using AgenticDesignPatterns.Shared;
using Chapter17.Reasoning;
using Chapter17.Reasoning.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var settings = HostedFoundryConfiguration.LoadSettings(builder.Configuration);
var deploymentAlias = builder.Configuration["deployment"];
var deployment = settings.ResolveModelDeployment(deploymentAlias);
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
});
IChatClient chatClient = builder.WebApplicationBuilder.Environment.IsDevelopment()
    && !string.IsNullOrWhiteSpace(settings.ApiKey)
    ? ReasoningChatClientFactory.CreateWithApiKey(settings, deploymentAlias)
    : ReasoningChatClientFactory.CreateWithCredential(settings, deployment, credential);
using IChatClient modelClient = chatClient;
builder.ConfigureTracing(tracing => tracing.AddSource(ReasoningWorkflow.TelemetrySourceName));
builder.Services.AddFoundryResponses(new ReasoningHostedAgent(modelClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
