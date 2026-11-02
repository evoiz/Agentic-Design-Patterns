using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Chapter09.LearningAdaptation;
using Chapter09.LearningAdaptation.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
using IChatClient chatClient = HostedFoundryConfiguration.CreateChatClient(
    builder.Configuration,
    builder.Configuration["deployment"]);
builder.ConfigureTracing(tracing => tracing.AddSource(LearningAdaptationWorkflow.TelemetrySourceName));
builder.Services.AddFoundryResponses(new LearningAdaptationAgent(chatClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
