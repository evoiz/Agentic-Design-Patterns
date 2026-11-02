using AgenticDesignPatterns.Shared;
using Chapter03.Parallelization;
using Microsoft.Extensions.AI;

var demos = new Dictionary<string, (string Name, ParallelizationScenario Scenario, string Input)>(
    StringComparer.OrdinalIgnoreCase)
{
    ["research"] = ("Sustainable technology research", ParallelizationScenario.SustainableTechnology,
        "Summarize sustainable technology progress and climate solutions."),
    ["topic"] = ("Topic analysis", ParallelizationScenario.TopicAnalysis, "The history of space exploration")
};

string? demoSelection = null;
string? deploymentSelection = null;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (deploymentSelection is not null ||
            index + 1 >= args.Length ||
            string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        deploymentSelection = args[++index];
    }
    else if (demoSelection is null)
    {
        demoSelection = args[index];
    }
    else
    {
        invalidArguments = true;
        break;
    }
}

var runAllDemos = demoSelection is null ||
    string.Equals(demoSelection, "all", StringComparison.OrdinalIgnoreCase);
var selectedDemos = runAllDemos
    ? demos
    : demoSelection is not null && demos.TryGetValue(demoSelection, out var selected)
        ? new Dictionary<string, (string Name, ParallelizationScenario Scenario, string Input)>(StringComparer.OrdinalIgnoreCase)
        {
            [demoSelection] = selected
        }
        : null;

if (invalidArguments || selectedDemos is null)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/01-Flow/Chapter03.Parallelization [all|research|topic] [--deployment <name>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using var chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentSelection);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentSelection)}");

    foreach (var (key, demo) in selectedDemos)
    {
        Console.WriteLine($"\n=== {demo.Name} ({key}) ===");
        await ParallelizationWorkflow.RunAsync(chatClient, demo.Scenario, demo.Input, Console.WriteLine);
    }
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
