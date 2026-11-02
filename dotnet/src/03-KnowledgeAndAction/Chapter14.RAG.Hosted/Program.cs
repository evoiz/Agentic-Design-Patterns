using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Chapter14.RAG;
using Chapter14.RAG.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
using IChatClient chatClient = HostedFoundryConfiguration.CreateChatClient(
    builder.Configuration, builder.Configuration["deployment"]);
builder.ConfigureTracing(tracing => tracing.AddSource(RagAgent.TelemetrySourceName));
builder.Services.AddFoundryResponses(new RagHostedAgent(chatClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
