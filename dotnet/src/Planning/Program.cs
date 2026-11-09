using AgenticDesignPatterns.Shared;
using Planning;
using Microsoft.Extensions.AI;

const string defaultTopic = "Outline a safe migration from a legacy service to a supported platform.";
const string defaultResearchQuestion = "Research the economic impact of semaglutide on global healthcare systems.";
var selection = "plan-write";
string? task = null;
string? deploymentAlias = null;
var researchDeploymentAlias = "deep-research";
var invalidArguments = false;
var selectionSeen = false;
var researchDeploymentSeen = false;

for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--task", StringComparison.OrdinalIgnoreCase))
    {
        if (task is not null || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        task = args[++index];
    }
    else if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (deploymentAlias is not null || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

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
    else if (!selectionSeen &&
             args[index] is "plan-write" or "deep-research" or "all")
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
        "Usage: dotnet run --project dotnet/src/Planning " +
        "[plan-write|deep-research|all] [--task <text>] " +
        "[--deployment <chat-model-alias>] [--research-deployment <deep-research-alias>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    if (selection is "plan-write" or "all")
    {
        using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
        var (planner, writer) = PlanningAgents.Create(chatClient);
        var result = await PlanningWorkflow.RunAsync(
            planner,
            writer,
            task ?? defaultTopic,
            Console.WriteLine);
        Console.WriteLine("\n--- Final output ---");
        Console.WriteLine(result.Format());
    }

    if (selection is "deep-research" or "all")
    {
        await DeepResearchDemo.RunAsync(settings, researchDeploymentAlias, task ?? defaultResearchQuestion);
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

return 0;
