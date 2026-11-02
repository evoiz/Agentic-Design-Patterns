using AgenticDesignPatterns.Shared;
using Azure.Core;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace Chapter17.Reasoning;

public static class ReasoningChatClientFactory
{
    public static IChatClient CreateWithApiKey(FoundrySettings settings, string? deploymentAlias = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("Foundry:ApiKey is required for the console reasoning demos.");
        }

        var deployment = settings.ResolveModelDeployment(deploymentAlias);
        var client = new OpenAIClient(
            new ApiKeyCredential(settings.ApiKey),
            new OpenAIClientOptions { Endpoint = settings.OpenAIEndpoint });
#pragma warning disable OPENAI001
        var chatClient = client.GetResponsesClient().AsIChatClient(deployment);
#pragma warning restore OPENAI001
        return chatClient;
    }

    public static IChatClient CreateWithCredential(
        FoundrySettings settings,
        string deployment,
        TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);
        ArgumentNullException.ThrowIfNull(credential);

#pragma warning disable OPENAI001
        var client = new ResponsesClient(
            authenticationPolicy: new BearerTokenPolicy(credential, "https://ai.azure.com/.default"),
            options: new ResponsesClientOptions { Endpoint = settings.OpenAIEndpoint });
        var chatClient = client.AsIChatClient(deployment);
#pragma warning restore OPENAI001
        return chatClient;
    }
}
