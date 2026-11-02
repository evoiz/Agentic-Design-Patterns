using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Chapter10.MCP.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

const string TelemetrySourceName = "Chapter10.MCP";
var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var serverUrl = builder.Configuration["CHAPTER10_MCP_SERVER_URL"];
if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var mcpServerEndpoint) ||
    mcpServerEndpoint.Scheme is not ("http" or "https"))
{
    throw new InvalidOperationException(
        "Set CHAPTER10_MCP_SERVER_URL to the absolute HTTP(S) endpoint of a reachable Streamable HTTP MCP server.");
}

using IChatClient chatClient = HostedFoundryConfiguration
    .CreateChatClient(builder.Configuration, builder.Configuration["deployment"])
    .AsBuilder()
    .UseOpenTelemetry(sourceName: TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
    .Build();
builder.ConfigureTracing(tracing => tracing.AddSource(TelemetrySourceName));
builder.Services.AddFoundryResponses(new McpHostedAgent(chatClient, mcpServerEndpoint));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
