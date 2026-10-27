using AgenticDesignPatterns.Shared;
using Chapter17.Reasoning;
using Microsoft.Extensions.AI;

var demos = new Dictionary<string, (string Name, ReasoningScenario Scenario, string Input)>(
    StringComparer.OrdinalIgnoreCase)
{
    ["cot"] = (
        "Reasoning prompt with a concise rationale",
        ReasoningScenario.ChainOfThought,
        "Explain the main differences between classical computers and quantum computers, and briefly describe a potential application of quantum computing."),
    ["self-correction"] = (
        "Bounded self-correction",
        ReasoningScenario.SelfCorrection,
        """
        Original prompt: Write a short, engaging social media post (maximum 150 characters) announcing a new eco-friendly product line: "GreenTech Gadgets".

        Initial draft: We have new products. They are green and techy. Buy GreenTech Gadgets now!
        """),
    ["react"] = (
        "ReAct with a deterministic factorial tool",
        ReasoningScenario.ReAct,
        "Calculate 10 factorial and briefly explain the result."),
    ["deep-search"] = (
        "Bounded query planning, reflection, and synthesis",
        ReasoningScenario.DeepSearch,
        "Explain the main differences between classical computers and quantum computers, and briefly describe a potential application of quantum computing.")
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
        ? new Dictionary<string, (string Name, ReasoningScenario Scenario, string Input)>(
            StringComparer.OrdinalIgnoreCase)
        {
            [demoSelection] = selected
        }
        : null;

if (invalidArguments ||
    (selectedDemos is null &&
     demoSelection?.ToLowerInvariant() is not ("foundry-web-search" or "foundry-code-interpreter")))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/02-ReasonAndImprove/Chapter17.Reasoning " +
        "[all|cot|self-correction|react|deep-search|foundry-web-search|foundry-code-interpreter] [--deployment <name>]");
    return 1;
}

try
{
    if (selectedDemos is null)
    {
        var toolName = demoSelection!.ToLowerInvariant() switch
        {
            "foundry-web-search" => "web-search",
            "foundry-code-interpreter" => "code-interpreter",
            _ => throw new InvalidOperationException("Unknown Foundry-native reasoning demo.")
        };
        await FoundryNativeDemo.RunAsync(toolName, deploymentSelection);
        return 0;
    }

    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = ReasoningChatClientFactory.CreateWithApiKey(settings, deploymentSelection);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentSelection)}");

    foreach (var (key, demo) in selectedDemos)
    {
        Console.WriteLine($"\n=== {demo.Name} ({key}) ===");
        await ReasoningWorkflow.RunAsync(chatClient, demo.Scenario, demo.Input, Console.WriteLine);
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
