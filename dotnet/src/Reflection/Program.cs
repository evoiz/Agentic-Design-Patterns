using AgenticDesignPatterns.Shared;
using Reflection;
using Microsoft.Extensions.AI;

var demos = new Dictionary<string, (string Name, ReflectionScenario Scenario, string Input)>(
    StringComparer.OrdinalIgnoreCase)
{
    ["csharp-review"] = (
        "Review and refine a C# method",
        ReflectionScenario.CSharpCodeReview,
        ReflectionDemos.CSharpReviewSampleTask),
    ["fact-check"] = (
        "Generate and independently fact-check a paragraph",
        ReflectionScenario.FactCheck,
        "The role of photosynthesis in plants"),
    ["product"] = (
        "Critique and refine a product description",
        ReflectionScenario.ProductDescription,
        "A mug that keeps coffee hot and can be controlled by a smartphone app."),
    ["code-review"] = (
        "Iteratively review and refine Python code",
        ReflectionScenario.IterativeCodeReview,
        ReflectionDemos.FactorialSampleTask)
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

var runAllDemos = string.Equals(demoSelection, "all", StringComparison.OrdinalIgnoreCase);
var selectedDemoName = demoSelection ?? "csharp-review";
var selectedDemos = runAllDemos
    ? demos
    : demos.TryGetValue(selectedDemoName, out var selected)
        ? new Dictionary<string, (string Name, ReflectionScenario Scenario, string Input)>(
            StringComparer.OrdinalIgnoreCase)
        {
            [selectedDemoName] = selected
        }
        : null;

if (invalidArguments || selectedDemos is null)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/Reflection [csharp-review|fact-check|product|code-review|all] [--deployment <name>]");
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
        await ReflectionDemos.RunAsync(chatClient, demo.Scenario, demo.Input, Console.WriteLine);
    }
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
