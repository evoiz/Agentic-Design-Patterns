using AgenticDesignPatterns.Shared;
using Azure.AI.AgentServer.Core;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter02.Routing;
using Chapter02.Routing.Hosted;
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
IChatClient modelClient = string.IsNullOrWhiteSpace(settings.ApiKey)
    ? new AzureOpenAIClient(
            new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)), credential)
        .GetChatClient(deployment)
        .AsIChatClient()
    : FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
using IChatClient chatClient = modelClient.AsBuilder()
    .UseOpenTelemetry(sourceName: RoutingWorkflow.TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
    .Build();

builder.ConfigureTracing(tracing => tracing.AddSource(RoutingWorkflow.TelemetrySourceName));
builder.Services.AddFoundryResponses(new RoutingAgent(chatClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
