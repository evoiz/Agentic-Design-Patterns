using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Azure.AI.AgentServer.Core;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter07.MultiAgent;
using Chapter07.MultiAgent.Distributed;
using Chapter07.MultiAgent.Hosted;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var configuration = builder.Configuration;
var role = configuration["Distributed:Role"]
    ?? throw new InvalidOperationException("Set Distributed__Role to researcher, writer, or orchestrator.");
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
});
using var httpClient = new HttpClient(new FoundryCallIdHandler(
    new HttpClientHandler { AllowAutoRedirect = false }))
{
    Timeout = TimeSpan.FromMinutes(3)
};
IChatClient? modelClient = null;
try
{
    Func<string, CancellationToken, Task<string>> execute;
    if (role == "orchestrator")
    {
        var researcher = new RemoteSpecialist(httpClient,
            configuration["Distributed:ResearcherEndpoint"]
                ?? throw new InvalidOperationException("Set Distributed__ResearcherEndpoint to its full Responses URL."),
            credential);
        var writer = new RemoteSpecialist(httpClient,
            configuration["Distributed:WriterEndpoint"]
                ?? throw new InvalidOperationException("Set Distributed__WriterEndpoint to its full Responses URL."),
            credential);
        execute = (input, token) => MultiAgentWorkflow.RunCrewAsync(
            researcher.InvokeAsync,
            writer.InvokeAsync,
            input,
            cancellationToken: token);
    }
    else if (role is "researcher" or "writer")
    {
        var settings = HostedFoundryConfiguration.LoadSettings(configuration);
        // Hosted runtimes use managed identity even if a developer configured an API key locally.
        var hosted = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT"));
        modelClient = !hosted && !string.IsNullOrWhiteSpace(settings.ApiKey)
            ? FoundryChatClientFactory.CreateChatClient(settings)
            : new AzureOpenAIClient(
                    new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)), credential)
                .GetChatClient(settings.ModelDeployment)
                .AsIChatClient();
        var client = modelClient;
        execute = async (input, token) =>
            (await MultiAgentWorkflow.CreateCrewSpecialist(client, role)
                .RunAsync(input, cancellationToken: token)).Text;
    }
    else
    {
        throw new InvalidOperationException($"Unknown distributed role '{role}'. Select researcher, writer, or orchestrator.");
    }

    AIAgent agent = new StatelessRequestAgent($"chapter07-{role}", execute)
        .AsBuilder()
        .UseOpenTelemetry(MultiAgentWorkflow.TelemetrySourceName,
            configure: options => options.EnableSensitiveData = true)
        .Build();
    builder.ConfigureTracing(tracing => tracing.AddSource(MultiAgentWorkflow.TelemetrySourceName));
#pragma warning disable MAAI001
    builder.Services.AddSingleton<HostedSessionIsolationKeyProvider, RequestIsolationKeyProvider>();
#pragma warning restore MAAI001
    builder.Services.AddFoundryResponses(agent);
    builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
    var app = builder.Build();
    app.Run();
}
finally
{
    modelClient?.Dispose();
}
