using AgenticDesignPatterns.Shared;
using Chapter12.ExceptionHandling;
using Microsoft.Extensions.AI;

var demos = new Dictionary<string, (string Name, string Input)>(StringComparer.OrdinalIgnoreCase)
{
    ["primary"] = (
        "Precise lookup succeeds",
        "1600 Amphitheatre Parkway, Mountain View, CA"),
    ["retry"] = (
        "Transient failure retries the primary lookup",
        "transient: 1600 Amphitheatre Parkway, Mountain View, CA"),
    ["fallback"] = (
        "Unavailable precise lookup uses area fallback",
        "primary-unavailable: 1 Market Street, San Francisco, CA")
};

string? selection = null;
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
    else if (selection is null)
    {
        selection = args[index];
    }
    else
    {
        invalidArguments = true;
        break;
    }
}

var selectedDemos = selection is null || string.Equals(selection, "all", StringComparison.OrdinalIgnoreCase)
    ? demos
    : selection is not null && demos.TryGetValue(selection, out var selected)
        ? new Dictionary<string, (string Name, string Input)>(StringComparer.OrdinalIgnoreCase)
        {
            [selection] = selected
        }
        : null;

if (invalidArguments || selectedDemos is null)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/05-TrustAndOperations/Chapter12.ExceptionHandling " +
        "[all|primary|retry|fallback] [--deployment <name>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentSelection);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentSelection)}");

    foreach (var (key, demo) in selectedDemos)
    {
        Console.WriteLine($"\n=== {demo.Name} ({key}) ===");
        Console.WriteLine($"\nUser: {demo.Input}");
        await ExceptionHandlingWorkflow.RunAsync(chatClient, demo.Input, Console.WriteLine);
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
