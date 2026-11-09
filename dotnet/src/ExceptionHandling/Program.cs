using AgenticDesignPatterns.Shared;
using ExceptionHandling;
using Microsoft.Extensions.AI;

(string Key, string Name, string Input)[] demos =
[
    ("primary", "Precise lookup succeeds", "1600 Amphitheatre Parkway, Mountain View, CA"),
    ("retry", "Transient failure retries the primary lookup", "transient: 1600 Amphitheatre Parkway, Mountain View, CA"),
    ("fallback", "Unavailable precise lookup uses area fallback", "primary-unavailable: 1 Market Street, San Francisco, CA")
];

string? selection = null;
string? deployment = null;
var valid = true;
for (var index = 0; index < args.Length && valid; index++)
{
    if (args[index].Equals("--deployment", StringComparison.OrdinalIgnoreCase))
    {
        valid = deployment is null && index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]);
        deployment = valid ? args[++index] : null;
    }
    else
    {
        valid = selection is null;
        selection = args[index];
    }
}

selection ??= "primary";
var selected = demos
    .Where(demo => selection is null || selection.Equals("all", StringComparison.OrdinalIgnoreCase) ||
        demo.Key.Equals(selection, StringComparison.OrdinalIgnoreCase))
    .ToArray();
if (!valid || selected.Length == 0)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/ExceptionHandling [all|primary|retry|fallback] [--deployment <name>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deployment);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deployment)}");
    foreach (var demo in selected)
    {
        Console.WriteLine($"\n=== {demo.Name} ({demo.Key}) ===\n\nUser: {demo.Input}");
        await ExceptionHandlingWorkflow.RunAsync(chatClient, demo.Input, Console.WriteLine);
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
