using AgenticDesignPatterns.Shared;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter19.Evaluation;
using Chapter19.Evaluation.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var configuration = builder.Configuration;
var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"] ?? configuration["Foundry:ProjectEndpoint"];
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"] ?? configuration["Foundry:ModelDeployment"];
if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
{
    throw new InvalidOperationException(
        "Set FOUNDRY_PROJECT_ENDPOINT and AZURE_AI_MODEL_DEPLOYMENT_NAME, " +
        "or configure shared Foundry:ProjectEndpoint and Foundry:ModelDeployment settings. " +
        "The hosted judge always uses Entra ID, never the console API key.");
}
var settings = new FoundrySettings { ProjectEndpoint = endpoint, ModelDeployment = deployment, ApiKey = string.Empty };
using IChatClient model = new AzureOpenAIClient(
        new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)),
        new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = configuration["AZURE_TENANT_ID"]
        }))
    .GetChatClient(deployment)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(sourceName: LegalSurveyJudge.TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
    .Build();
builder.ConfigureTracing(tracing => tracing.AddSource(LegalSurveyJudge.TelemetrySourceName));
builder.Services.AddFoundryResponses(new SurveyEvaluationAgent(model));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
builder.Build().Run();
