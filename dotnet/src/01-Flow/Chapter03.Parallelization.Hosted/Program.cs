using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Chapter03.Parallelization;
using Chapter03.Parallelization.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
using IChatClient modelClient = HostedFoundryConfiguration.CreateChatClient(
    builder.Configuration, builder.Configuration["deployment"]);
builder.ConfigureTracing(tracing => tracing.AddSource(ParallelizationWorkflow.TelemetrySourceName));
builder.Services.AddFoundryResponses(new ParallelizationAgent(modelClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
