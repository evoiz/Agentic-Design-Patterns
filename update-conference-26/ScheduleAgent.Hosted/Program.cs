using AgenticDesignPatterns.Shared.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using ScheduleAgent.Hosted;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
// Ignore API-key configuration even when supplied locally; hosted inference uses Entra ID.
builder.WebApplicationBuilder.Configuration["Foundry:ApiKey"] = "";
using IChatClient client = HostedFoundryConfiguration.CreateChatClient(builder.Configuration);
const string telemetrySource = "UpdateConference.Schedule";
builder.ConfigureTracing(tracing => tracing.AddSource(telemetrySource));
using var agent = new OpenTelemetryAgent(new ScheduleHostedAgent(client), telemetrySource)
{
    EnableSensitiveData = builder.Configuration.GetValue<bool>("EnableSensitiveTelemetry")
};
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
