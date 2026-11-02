using AgenticDesignPatterns.Shared;
using Chapter18.Guardrails.Demos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;

var demos = new Dictionary<string, (string Name, Func<IChatClient, string?, Task> Run)>(
    StringComparer.OrdinalIgnoreCase)
{
    ["policy"] = ("LLM policy guardrail", (client, text) => PolicyGuardrailDemo.RunAsync(client, text)),
    ["tool-validation"] = ("Tool parameter validation", (client, _) => ToolParameterValidationDemo.RunAsync(client))
};
const string NativeDemoName = "Foundry Content Safety";
Func<string?, Task> runNativeDemo = text => ContentSafetyDemo.RunAsync(LoadConfiguration(), text);

string? demoSelection = null;
string? deploymentSelection = null;
string? textInput = null;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (deploymentSelection is not null || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        deploymentSelection = args[++index];
    }
    else if (string.Equals(args[index], "--text", StringComparison.OrdinalIgnoreCase))
    {
        if (textInput is not null || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        textInput = args[++index];
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

var runAllDemos = demoSelection is null || string.Equals(demoSelection, "all", StringComparison.OrdinalIgnoreCase);
var selectedDemos = runAllDemos
    ? demos
    : demoSelection is not null && demos.TryGetValue(demoSelection, out var selected)
        ? new Dictionary<string, (string Name, Func<IChatClient, string?, Task> Run)>(StringComparer.OrdinalIgnoreCase)
        {
            [demoSelection] = selected
        }
        : null;
var selectedNative = runAllDemos ||
    string.Equals(demoSelection, "content-safety", StringComparison.OrdinalIgnoreCase);

if (invalidArguments ||
    (selectedDemos is null && !selectedNative) ||
    (selectedNative && !runAllDemos && deploymentSelection is not null))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/05-TrustAndOperations/Chapter18.Guardrails [all|policy|tool-validation|content-safety] " +
        "[--deployment <name>] [--text <text>]");
    return 1;
}

try
{
    if (selectedDemos is not null)
    {
        var settings = FoundryChatClientFactory.LoadSettings();
        using var chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentSelection);
        Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentSelection)}");

        foreach (var (key, demo) in selectedDemos)
        {
            Console.WriteLine($"\n=== {demo.Name} ({key}) ===");
            await demo.Run(chatClient, textInput);
        }
    }

    if (selectedNative)
    {
        Console.WriteLine($"\n=== {NativeDemoName} (content-safety) ===");
        await runNativeDemo(textInput);
    }
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;

static IConfiguration LoadConfiguration() =>
    new ConfigurationBuilder()
        .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
        .AddEnvironmentVariables()
        .Build();
