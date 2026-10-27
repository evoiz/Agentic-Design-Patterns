using AgenticDesignPatterns.Shared;
using Chapter03.Parallelization;
using Microsoft.Extensions.AI;

var demos = new Dictionary<string, (string Name, Func<IChatClient, Task> Run)>(
    StringComparer.OrdinalIgnoreCase)
{
    ["research"] = ("Sustainable technology research", ParallelizationDemo.RunSustainableTechnologyAsync),
    ["topic"] = ("Topic analysis", ParallelizationDemo.RunTopicAnalysisAsync)
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
        ? new Dictionary<string, (string Name, Func<IChatClient, Task> Run)>(StringComparer.OrdinalIgnoreCase)
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
    var chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentSelection);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentSelection)}");

    foreach (var (key, demo) in selectedDemos)
    {
        Console.WriteLine($"\n=== {demo.Name} ({key}) ===");
        await demo.Run(chatClient);
    }
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
