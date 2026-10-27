using AgenticDesignPatterns.Shared;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter10.MCP;
using Chapter10.MCP.Hosted;
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
var serverUrl = builder.Configuration["CHAPTER10_MCP_SERVER_URL"];
if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var mcpServerEndpoint) ||
    mcpServerEndpoint.Scheme is not ("http" or "https"))
{
    throw new InvalidOperationException(
        "Set CHAPTER10_MCP_SERVER_URL to the absolute HTTP(S) endpoint of a reachable Streamable HTTP MCP server.");
}

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
builder.ConfigureTracing(tracing => tracing.AddSource(McpAgent.TelemetrySourceName));
builder.Services.AddFoundryResponses(new McpHostedAgent(modelClient, mcpServerEndpoint));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
