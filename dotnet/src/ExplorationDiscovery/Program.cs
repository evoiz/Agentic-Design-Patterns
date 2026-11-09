using AgenticDesignPatterns.Shared;
using ExplorationDiscovery;
using Microsoft.Extensions.AI;

const string defaultTopic = "Explore whether adaptive batching can reduce inference cost without harming tail latency.";
var selection = "explore";
var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var researchDomains = new List<string>();
var selectionSeen = false;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    var option = args[index];
    if (!selectionSeen && option is "explore" or "deep-research")
    {
        selection = option;
        selectionSeen = true;
        continue;
    }
    if (option.ToLowerInvariant() is not ("--topic" or "--deployment" or "--research-deployment" or "--domain") ||
        index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
    {
        invalidArguments = true;
        break;
    }
    var value = args[++index];
    if (option.Equals("--domain", StringComparison.OrdinalIgnoreCase))
        researchDomains.Add(value);
    else if (!options.TryAdd(option, value))
    {
        invalidArguments = true;
        break;
    }
}

if (invalidArguments)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/ExplorationDiscovery " +
        "[explore|deep-research] [--topic <text>] [--deployment <chat-model-alias>] " +
        "[--research-deployment <deep-research-alias>] [--domain <host> ...]");
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    var request = options.GetValueOrDefault("--topic") ?? defaultTopic;
    var deploymentAlias = options.GetValueOrDefault("--deployment");
    if (request.Length > ExplorationWorkflow.MaxTopicLength)
    {
        throw new InvalidOperationException(
            $"Research topics must be at most {ExplorationWorkflow.MaxTopicLength} characters.");
    }

    if (selection == "explore")
    {
        using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
        var result = await ExplorationWorkflow.RunAsync(
            chatClient, request, Console.WriteLine, cancellation.Token);
        Console.WriteLine("\n--- Final exploration result ---");
        Console.WriteLine(result.Format());
    }
    else
    {
        await DeepResearchDemo.RunAsync(
            settings, options.GetValueOrDefault("--research-deployment") ?? "deep-research",
            request, researchDomains, cancellation.Token);
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or TimeoutException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"Foundry request failed: {exception.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Exploration canceled.");
    return 1;
}

return 0;
