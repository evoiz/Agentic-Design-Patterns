using AgenticDesignPatterns.Shared;
using Chapter21.ExplorationDiscovery;
using Microsoft.Extensions.AI;

const string defaultTopic = "Explore whether adaptive batching can reduce inference cost without harming tail latency.";
var selection = "explore";
string? topic = null;
string? deploymentAlias = null;
var researchDeploymentAlias = "deep-research";
var researchDomains = new List<string>();
var selectionSeen = false;
var deploymentSeen = false;
var researchDeploymentSeen = false;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--topic", StringComparison.OrdinalIgnoreCase))
    {
        if (topic is not null || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        topic = args[++index];
    }
    else if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (deploymentSeen || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        deploymentSeen = true;
        deploymentAlias = args[++index];
    }
    else if (string.Equals(args[index], "--research-deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (researchDeploymentSeen ||
            index + 1 >= args.Length ||
            string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        researchDeploymentSeen = true;
        researchDeploymentAlias = args[++index];
    }
    else if (string.Equals(args[index], "--domain", StringComparison.OrdinalIgnoreCase))
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        researchDomains.Add(args[++index]);
    }
    else if (!selectionSeen && args[index] is "explore" or "deep-research")
    {
        selection = args[index];
        selectionSeen = true;
    }
    else
    {
        invalidArguments = true;
        break;
    }
}

if (invalidArguments)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/02-ReasonAndImprove/Chapter21.ExplorationDiscovery " +
        "[explore|deep-research] [--topic <text>] [--deployment <chat-model-alias>] " +
        "[--research-deployment <deep-research-alias>] [--domain <host> ...]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    var request = topic ?? defaultTopic;
    if (request.Length > ExplorationWorkflow.MaxTopicLength)
    {
        throw new InvalidOperationException(
            $"Research topics must be at most {ExplorationWorkflow.MaxTopicLength} characters.");
    }

    if (selection == "explore")
    {
        using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
        var result = await ExplorationWorkflow.RunAsync(chatClient, request, Console.WriteLine);
        Console.WriteLine("\n--- Final exploration result ---");
        Console.WriteLine(result.Format());
    }
    else
    {
        if (researchDomains.Count == 0)
        {
            throw new InvalidOperationException(
                "Foundry Deep Research requires one or more --domain values to bound public web search.");
        }

        if (!settings.ModelDeployments.Keys.Any(alias =>
                string.Equals(alias, researchDeploymentAlias, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"The Foundry Deep Research deployment alias '{researchDeploymentAlias}' is not configured. " +
                "Add Foundry:ModelDeployments:deep-research to shared user-secrets or " +
                "Foundry__ModelDeployments__deep-research to the environment, then use " +
                "--research-deployment deep-research.");
        }

        Console.WriteLine("\n=== Foundry Deep Research ===");
        await FoundryDeepResearch.RunAsync(
            settings,
            researchDeploymentAlias,
            request,
            Console.WriteLine,
            researchInstructions: """
                You are a research analyst exploring an open-ended question using public web sources.
                Search within the requested topic, compare source quality, and return a concise,
                citation-rich discovery report. Separate sourced findings from uncertainty.
                Search only within the configured allowed domains.
                Do not execute code, make external changes, or present hypotheses as experimental results.
                """,
            allowedDomains: researchDomains);
    }
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"Foundry request failed: {exception.Message}");
    return 1;
}
catch (TimeoutException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
