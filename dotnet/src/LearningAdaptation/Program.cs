using AgenticDesignPatterns.Shared;
using Azure.Identity;
using LearningAdaptation;
using Microsoft.Extensions.AI;

// Usage: [maf|foundry|all] [--iterations 1-3] [--deployment <deployment-alias>]
var selection = "maf";
string? deploymentAlias = null;
var iterations = 1;
var selectionSeen = false;
var valid = true;
for (var index = 0; index < args.Length && valid; index++)
{
    var hasValue = index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]);
    switch (args[index].ToLowerInvariant())
    {
        case "--deployment" when hasValue && deploymentAlias is null:
            deploymentAlias = args[++index];
            break;
        case "--iterations" when hasValue && int.TryParse(args[index + 1], out iterations) &&
                                 iterations is >= 1 and <= LearningAdaptationWorkflow.MaximumIterations:
            index++;
            break;
        case "maf" or "foundry" or "all" when !selectionSeen:
            selection = args[index].ToLowerInvariant();
            selectionSeen = true;
            break;
        default:
            valid = false;
            break;
    }
}

if (!valid)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/LearningAdaptation " +
        "[maf|foundry|all] [--iterations 1-3] [--deployment <deployment-alias>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");

    var result = await LearningAdaptationWorkflow.RunAsync(
        chatClient,
        LearningAdaptationWorkflow.InitialInstructions,
        iterations,
        Console.WriteLine);
    Console.WriteLine("\n--- Adaptation result ---");
    Console.WriteLine(result.Format());

    if (selection is "foundry" or "all")
    {
        Console.WriteLine("\n--- Foundry cloud evaluation ---");
        await FoundryEvaluationDemo.RunAsync(
            settings,
            settings.ResolveModelDeployment(deploymentAlias),
            result.Baseline.Instructions,
            result.Best.Instructions,
            Console.WriteLine);
    }
}
catch (AuthenticationFailedException exception)
{
    Console.Error.WriteLine(
        $"Foundry evaluation requires an authenticated Microsoft Entra ID session. Sign in with az login and retry. {exception.Message}");
    return 1;
}
catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TimeoutException)
{
    Console.Error.WriteLine(exception is HttpRequestException
        ? $"Foundry request failed: {exception.Message}"
        : exception.Message);
    return 1;
}

return 0;
