using AgenticDesignPatterns.Shared;
using Chapter14.RAG;
using Microsoft.Extensions.AI;

RagCommandLineOptions options;
try
{
    options = RagCommandLineOptions.Parse(args);
}
catch (ArgumentException)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/03-KnowledgeAndAction/Chapter14.RAG " +
        "[rag|foundry-files|foundry-web|all] [--deployment <name>] [question]");
    return 1;
}

var (mode, deploymentAlias, question) = options;

try
{
    if (mode.Equals("rag", StringComparison.OrdinalIgnoreCase) ||
        mode.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        var settings = FoundryChatClientFactory.LoadSettings();
        using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        Console.WriteLine($"Model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
        Console.WriteLine($"\nUser: {question}");
        Console.WriteLine("\n--- Retrieved context ---");
        var answer = await RagAgent.RunAsync(chatClient, question, Console.WriteLine);
        Console.WriteLine("\n--- Final grounded response ---");
        Console.WriteLine(answer);
    }

    if (mode.Equals("foundry-files", StringComparison.OrdinalIgnoreCase) ||
        mode.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\n--- Foundry-native File Search ---");
        Console.WriteLine(await FoundryNativeDemo.RunAsync("foundry-files", question, deploymentAlias));
    }

    if (mode.Equals("foundry-web", StringComparison.OrdinalIgnoreCase) ||
        mode.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\n--- Foundry-native Web Search ---");
        Console.WriteLine(await FoundryNativeDemo.RunAsync("foundry-web", question, deploymentAlias));
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
