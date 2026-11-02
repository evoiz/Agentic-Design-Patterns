using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.Shared.Hosting;
using Azure.AI.AgentServer.Invocations;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter13.HumanInTheLoop;
using Chapter13.HumanInTheLoop.Hosted;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();
var settings = HostedFoundryConfiguration.LoadSettings(configuration);

// Identity-only by design: a configured API key is ignored so approvals always run under a managed/developer identity.
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
});
using IChatClient chatClient = new AzureOpenAIClient(
        new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)), credential)
    .GetChatClient(settings.ResolveModelDeployment(configuration["deployment"] is { Length: > 0 } alias ? alias : null))
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(
        sourceName: CustomerSupportAgent.TelemetrySourceName,
        configure: options => options.EnableSensitiveData = true)
    .Build();

InvocationsServer.Run<CustomerSupportInvocationHandler>(configure: builder =>
{
    builder.Services.AddSingleton(chatClient);
});
