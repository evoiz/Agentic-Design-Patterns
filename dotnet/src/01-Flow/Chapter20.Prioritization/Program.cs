using AgenticDesignPatterns.Shared;
using Chapter20.Prioritization;
using Microsoft.Extensions.AI;

const string usage =
    "Usage: dotnet run --project dotnet/src/01-Flow/Chapter20.Prioritization " +
    "[--task <description> ...] [--deployment <name>]";

var tasks = new List<string>();
string? deploymentSelection = null;
for (var index = 0; index < args.Length; index++)
{
    var option = args[index].ToLowerInvariant();
    if (option is not ("--task" or "--deployment") ||
        index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) ||
        (option == "--deployment" && deploymentSelection is not null))
    {
        Console.Error.WriteLine(usage);
        return 1;
    }
    var value = args[++index];
    if (option == "--task") tasks.Add(value);
    else deploymentSelection = value;
}

if (tasks.Count > PrioritizationWorkflow.MaximumTasksPerRequest)
{
    Console.Error.WriteLine(usage);
    return 1;
}

if (tasks.Count == 0)
{
    tasks.AddRange(
    [
        "Create a task to implement a new login system. It's urgent and should be assigned to Worker B.",
        "Manage a new task: Review marketing website content."
    ]);
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentSelection);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentSelection)}");
    await PrioritizationWorkflow.RunAsync(chatClient, tasks, Console.WriteLine);
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
