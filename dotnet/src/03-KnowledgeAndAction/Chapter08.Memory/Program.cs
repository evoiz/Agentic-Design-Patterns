using AgenticDesignPatterns.Shared;
using Chapter08.Memory.Demos;
using Microsoft.Extensions.AI;

// Usage: [session|foundry-memory|all] [--deployment <model-alias>]
var deploymentIndex = Array.FindIndex(args, arg => arg.Equals("--deployment", StringComparison.OrdinalIgnoreCase));
var deploymentAlias = deploymentIndex >= 0 && deploymentIndex + 1 < args.Length ? args[deploymentIndex + 1] : null;
var selections = args.Where((_, index) => deploymentIndex < 0 || index < deploymentIndex || index > deploymentIndex + 1).ToArray();
var selection = selections.FirstOrDefault() ?? "session";
if (selections.Length > 1 ||
    selection is not ("session" or "foundry-memory" or "all") ||
    (deploymentIndex >= 0 && string.IsNullOrWhiteSpace(deploymentAlias)) ||
    args.Count(arg => arg.Equals("--deployment", StringComparison.OrdinalIgnoreCase)) > 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/03-KnowledgeAndAction/Chapter08.Memory " +
        "[session|foundry-memory|all] [--deployment <model-alias>]");
    return 1;
}

try
{
    if (selection is "session" or "all")
    {
        var settings = FoundryChatClientFactory.LoadSettings();
        using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
        await SessionMemoryDemo.RunAsync(chatClient, Console.WriteLine);
    }

    if (selection is "foundry-memory" or "all")
    {
        await FoundryMemoryDemo.RunAsync(deploymentAlias, Console.WriteLine);
    }
}
catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TimeoutException)
{
    Console.Error.WriteLine(exception is HttpRequestException
        ? $"Foundry request failed: {exception.Message}"
        : exception.Message);
    return 1;
}

return 0;
