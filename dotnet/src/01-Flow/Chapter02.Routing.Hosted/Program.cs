using AgenticDesignPatterns.Shared;
using Azure.AI.AgentServer.Core;
using AgenticDesignPatterns.Shared.Hosting;
using Chapter02.Routing;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
IChatClient modelClient = HostedFoundryConfiguration.CreateChatClient(
    builder.Configuration, builder.Configuration["deployment"]);
using IChatClient chatClient = modelClient.AsBuilder()
    .UseOpenTelemetry(sourceName: RoutingWorkflow.TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
    .Build();

builder.ConfigureTracing(tracing => tracing.AddSource(RoutingWorkflow.TelemetrySourceName));
builder.Services.AddFoundryResponses(new RoutingAgent(chatClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
