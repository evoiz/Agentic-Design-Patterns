using AgenticDesignPatterns.Shared;
using ToolUse;
using Microsoft.Extensions.AI;

var demos = new Dictionary<string, (string Name, ToolUseScenario Scenario, string[] Inputs)>(
    StringComparer.OrdinalIgnoreCase)
{
    ["search"] = (
        "Simulated information search",
        ToolUseScenario.Search,
        [
            "capital of France",
            "weather in London",
            "Tell me something about dogs."
        ]),
    ["calculator"] = (
        "Safe arithmetic calculator",
        ToolUseScenario.Calculator,
        [
            "Calculate the value of (5 + 7) * 3.",
            "What is 10 factorial?"
        ]),
    ["stocks"] = (
        "Simulated stock price lookup",
        ToolUseScenario.StockPrice,
        ["What is the simulated stock price for Apple (ticker: AAPL)?"]
    )
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

var runAllMafDemos = string.Equals(demoSelection, "all", StringComparison.OrdinalIgnoreCase);
var selectedDemoName = demoSelection ?? "calculator";
var selectedDemos = runAllMafDemos
    ? demos
    : demos.TryGetValue(selectedDemoName, out var selected)
        ? new Dictionary<string, (string Name, ToolUseScenario Scenario, string[] Inputs)>(
            StringComparer.OrdinalIgnoreCase)
        {
            [selectedDemoName] = selected
        }
        : null;

if (invalidArguments ||
    (selectedDemos is null &&
     demoSelection?.ToLowerInvariant() is not ("foundry-web" or "foundry-code" or "foundry-files")))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/ToolUse " +
        "[calculator|all|search|stocks|foundry-web|foundry-code|foundry-files] [--deployment <name>]");
    return 1;
}

try
{
    if (selectedDemos is null)
    {
        var toolName = demoSelection!.ToLowerInvariant() switch
        {
            "foundry-web" => "web",
            "foundry-code" => "code",
            "foundry-files" => "files",
            _ => throw new InvalidOperationException("Unknown Foundry-native demo selection.")
        };
        await FoundryNativeDemo.RunAsync(toolName);
        return 0;
    }

    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentSelection);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentSelection)}");

    foreach (var (key, demo) in selectedDemos)
    {
        Console.WriteLine($"\n=== {demo.Name} ({key}) ===");
        foreach (var input in demo.Inputs)
        {
            Console.WriteLine($"\nUser: {input}");
            var response = await ToolUseAgent.Create(
                    chatClient,
                    demo.Scenario,
                    Console.WriteLine)
                .RunAsync(input);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException($"The {key} tool-use demo returned an empty response.");
            }

            Console.WriteLine("\n--- Final agent response ---");
            Console.WriteLine(response.Text);
        }
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
