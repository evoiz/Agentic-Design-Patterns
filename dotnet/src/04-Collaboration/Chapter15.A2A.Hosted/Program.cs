using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using A2A;
using A2A.AspNetCore;
using Azure.Core;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter15.A2A;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Agents.AI.Hosting.A2A;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
// The deployed A2A server must never fall back to a configured local API key.
var hosted = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT"));
var settings = HostedFoundryConfiguration.LoadSettings(builder.Configuration);
using IChatClient modelClient = hosted
    ? new AzureOpenAIClient(
            new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)),
            new ManagedIdentityCredential(new ManagedIdentityCredentialOptions()))
        .GetChatClient(settings.ResolveModelDeployment())
        .AsIChatClient()
    : HostedFoundryConfiguration.CreateChatClient(builder.Configuration);
AIAgent agent = InterAgentAgent.Create(modelClient);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8088";
var publicUrl = builder.WebApplicationBuilder.Configuration["A2A_PUBLIC_URL"]
    ?? $"http://localhost:{port}/";
var agentCard = CreateAgentCard(publicUrl);

builder.WebApplicationBuilder.Services.AddHttpClient();
#pragma warning disable MEAI001
builder.WebApplicationBuilder.AddA2AServer(agent, options =>
{
    options.AgentRunMode = AgentRunMode.ReturnTaskWhen(
        (context, _) => ValueTask.FromResult(context.RequestContext.StreamingResponse));
});
#pragma warning restore MEAI001
builder.ConfigureTracing(tracing => tracing.AddSource(InterAgentAgent.TelemetrySourceName));

builder.RegisterProtocol("a2a", endpoints =>
{
    endpoints.MapA2AHttpJson(agent, "/");
    endpoints.MapA2AJsonRpc(agent, "/");
    endpoints.MapWellKnownAgentCard(agentCard);
});
var app = builder.Build();
await app.RunAsync();

static AgentCard CreateAgentCard(string agentUrl)
{
    if (!Uri.TryCreate(agentUrl, UriKind.Absolute, out var endpoint) ||
        endpoint.Scheme is not ("http" or "https"))
    {
        throw new InvalidOperationException("A2A_PUBLIC_URL must be an absolute HTTP or HTTPS URL.");
    }

    return new AgentCard
    {
        Name = InterAgentAgent.Name,
        Description = InterAgentAgent.Description,
        Version = "1.0.0",
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Capabilities = new AgentCapabilities
        {
            Streaming = true,
            PushNotifications = false
        },
        Skills =
        [
            new A2A.AgentSkill
            {
                Id = "weather-guidance",
                Name = "Weather guidance",
                Description = "Answers weather questions with general guidance, not live observations.",
                Tags = ["weather", "guidance"],
                Examples = ["What's the weather in Paris?"]
            }
        ],
        SupportedInterfaces =
        [
            new AgentInterface
            {
                Url = endpoint.ToString(),
                ProtocolBinding = ProtocolBindingNames.HttpJson,
                ProtocolVersion = "1.0"
            },
            new AgentInterface
            {
                Url = endpoint.ToString(),
                ProtocolBinding = ProtocolBindingNames.JsonRpc,
                ProtocolVersion = "1.0"
            }
        ]
    };
}
