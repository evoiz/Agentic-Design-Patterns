using AgenticDesignPatterns.Shared;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter18.Guardrails;
using Chapter18.Guardrails.Hosted;
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

IChatClient chatClient = string.IsNullOrWhiteSpace(settings.ApiKey)
    ? new AzureOpenAIClient(
            new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)),
            credential)
        .GetChatClient(deployment)
        .AsIChatClient()
    : FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
using IChatClient modelClient = chatClient;
builder.ConfigureTracing(tracing => tracing.AddSource(GuardrailsAgent.TelemetrySourceName));
#pragma warning disable MAAI001
builder.Services.AddSingleton<HostedSessionIsolationKeyProvider, RequestIsolationKeyProvider>();
#pragma warning restore MAAI001
builder.Services.AddFoundryResponses(new GuardrailsHostedAgent(modelClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
