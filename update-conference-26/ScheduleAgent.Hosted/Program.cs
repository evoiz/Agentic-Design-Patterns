using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;
using ScheduleAgent.Hosted;
using System.ClientModel.Primitives;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddUserSecrets(typeof(ScheduleHostedAgent).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
// Ignore API-key configuration even when supplied locally; hosted inference uses Entra ID.
builder.WebApplicationBuilder.Configuration["Foundry:ApiKey"] = "";

var settings = HostedFoundryConfiguration.LoadSettings(builder.Configuration);
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
});
#pragma warning disable OPENAI001 // The pinned SDK marks Entra authentication as experimental.
using IChatClient client = new OpenAIClient(
        new BearerTokenPolicy(credential, "https://ai.azure.com/.default"),
        new OpenAIClientOptions
        {
            Endpoint = new Uri($"{settings.ProjectEndpoint.TrimEnd('/')}/openai/v1/")
        })
    .GetChatClient(settings.ModelDeployment)
    .AsIChatClient();
#pragma warning restore OPENAI001
const string telemetrySource = "UpdateConference.Schedule";
builder.ConfigureTracing(tracing => tracing.AddSource(telemetrySource));

using var agent = new OpenTelemetryAgent(new ScheduleHostedAgent(client), telemetrySource)
{
    EnableSensitiveData = builder.Configuration.GetValue<bool?>("EnableSensitiveTelemetry") ?? false
};


builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
