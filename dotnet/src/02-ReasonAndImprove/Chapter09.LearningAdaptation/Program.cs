using AgenticDesignPatterns.Shared;
using Chapter09.LearningAdaptation;
using Chapter09.LearningAdaptation.Demos;
using Microsoft.Extensions.AI;
using Azure.Identity;

var selection = "maf";
var deploymentAlias = (string?)null;
var iterations = 1;
var selectionSeen = false;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (deploymentAlias is not null || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        deploymentAlias = args[++index];
    }
    else if (string.Equals(args[index], "--iterations", StringComparison.OrdinalIgnoreCase))
    {
        if (index + 1 >= args.Length ||
            !int.TryParse(args[index + 1], out iterations) ||
            iterations is < 1 or > LearningAdaptationWorkflow.MaximumIterations)
        {
            invalidArguments = true;
            break;
        }

        index++;
    }
    else if (!selectionSeen && args[index] is "maf" or "foundry" or "all")
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
        "Usage: dotnet run --project dotnet/src/02-ReasonAndImprove/Chapter09.LearningAdaptation " +
        "[maf|foundry|all] [--iterations 1-3] [--deployment <deployment-alias>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");

    var result = await LearningAdaptationDemo.RunAsync(
        chatClient,
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
catch (InvalidOperationException exception)
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
catch (AuthenticationFailedException exception)
{
    Console.Error.WriteLine(
        $"Foundry evaluation requires an authenticated Microsoft Entra ID session. Sign in with az login and retry. {exception.Message}");
    return 1;
}

return 0;
