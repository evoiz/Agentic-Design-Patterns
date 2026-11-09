using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using GoalSettingMonitoring;
using GoalSettingMonitoring.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var enableSensitiveTelemetry =
    builder.Configuration.GetValue<bool>("Foundry:Telemetry:IncludeSensitiveData");
using IChatClient chatClient = HostedFoundryConfiguration.CreateChatClient(
    builder.Configuration, builder.Configuration["deployment"]);
builder.ConfigureTracing(tracing => tracing.AddSource(GoalSettingWorkflow.TelemetrySourceName));
#pragma warning disable MAAI001
builder.Services.AddSingleton<HostedSessionIsolationKeyProvider, RequestIsolationKeyProvider>();
#pragma warning restore MAAI001
builder.Services.AddFoundryResponses(new GoalSettingAgent(chatClient, enableSensitiveTelemetry));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
