using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
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
var deploymentAlias = builder.Configuration["deployment"];
using IChatClient modelClient = HostedFoundryConfiguration.CreateChatClient(builder.Configuration, deploymentAlias);
builder.ConfigureTracing(tracing => tracing.AddSource(GuardrailsAgent.TelemetrySourceName));
#pragma warning disable MAAI001
builder.Services.AddSingleton<HostedSessionIsolationKeyProvider, RequestIsolationKeyProvider>();
#pragma warning restore MAAI001
builder.Services.AddFoundryResponses(new GuardrailsHostedAgent(modelClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
