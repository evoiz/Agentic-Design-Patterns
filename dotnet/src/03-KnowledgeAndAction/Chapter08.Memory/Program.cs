using AgenticDesignPatterns.Shared;
using Chapter08.Memory.Demos;
using Microsoft.Extensions.AI;

var selection = "session";
string? deploymentAlias = null;
var selectionSeen = false;
var deploymentSeen = false;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (deploymentSeen || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        deploymentSeen = true;
        deploymentAlias = args[++index];
    }
    else if (!selectionSeen &&
             args[index] is "session" or "foundry-memory" or "all")
    {
        selection = args[index];
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
