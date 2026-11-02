using AgenticDesignPatterns.Shared;
using Chapter07.MultiAgent;
using Microsoft.Extensions.AI;

const string defaultTask = "Explain how teams can use AI agents to analyze a product launch.";
var selection = "Crew";
string? task = null;
string? deploymentAlias = null;
var selectionSeen = false;
var invalidArguments = false;

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
    else if (!selectionSeen && MultiAgentWorkflow.TryParseScenario(args[index], out var scenario))
    {
        selection = scenario.ToString();
        selectionSeen = true;
    }
    else if (!selectionSeen &&
             string.Equals(args[index], "all", StringComparison.OrdinalIgnoreCase))
    {
        selection = "all";
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
        "Usage: dotnet run --project dotnet/src/04-Collaboration/Chapter07.MultiAgent " +
        "[sequential|parallel|loop|coordinator|agenttool|crew|all] " +
        "[--task <text>] [--deployment <deployment-alias>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");

    var input = task ?? defaultTask;
    var scenarios = selection == "all"
        ? Enum.GetValues<MultiAgentScenario>()
        : [Enum.Parse<MultiAgentScenario>(selection, ignoreCase: true)];
    foreach (var scenario in scenarios)
    {
        Console.WriteLine($"\n=== {scenario} ===");
        var result = await MultiAgentWorkflow.RunAsync(
            chatClient,
            scenario,
            input,
            Console.WriteLine);
        Console.WriteLine("\n--- Final output ---");
        Console.WriteLine(result);
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
