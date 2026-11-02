using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter20.Prioritization;
using Chapter20.Prioritization.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var deploymentAlias = builder.Configuration["deployment"];
using IChatClient chatClient = HostedFoundryConfiguration.CreateChatClient(builder.Configuration, deploymentAlias);

builder.ConfigureTracing(tracing => tracing.AddSource(PrioritizationWorkflow.TelemetrySourceName));
builder.Services.AddFoundryResponses(new PrioritizationAgent(chatClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
