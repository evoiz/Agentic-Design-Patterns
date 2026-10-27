using AgenticDesignPatterns.Shared;
using Azure.AI.AgentServer.Invocations;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter13.HumanInTheLoop;
using Chapter13.HumanInTheLoop.Hosted;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();
var settings = HostedFoundryConfiguration.LoadSettings(configuration);
var deployment = configuration["deployment"] is { Length: > 0 } alias
    ? settings.ResolveModelDeployment(alias)
    : settings.ModelDeployment;
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
});

IChatClient chatClient = new AzureOpenAIClient(
        new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)),
        credential)
    .GetChatClient(deployment)
    .AsIChatClient();
using IChatClient modelClient = chatClient.AsBuilder()
    .UseOpenTelemetry(
        sourceName: CustomerSupportAgent.TelemetrySourceName,
        configure: options => options.EnableSensitiveData = true)
    .Build();

InvocationsServer.Run<CustomerSupportInvocationHandler>(configure: builder =>
{
    builder.Services.AddSingleton<IChatClient>(modelClient);
});
