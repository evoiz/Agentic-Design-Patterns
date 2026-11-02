using System.Text.Json;
using AgenticDesignPatterns.Shared;
using Extension01.AgentHarness;

const string usage = "Usage: dotnet run --project dotnet\\src\\Extension01.AgentHarness " +
    "[--request <question>] [--deployment <alias>]";
string? request = null;
string? deployment = null;
for (var index = 0; index < args.Length; index++)
{
    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) ||
        args[index] is not ("--request" or "--deployment") ||
        (args[index] == "--request" && request is not null) ||
        (args[index] == "--deployment" && deployment is not null))
    {
        Console.Error.WriteLine(usage);
        return 1;
    }

    if (args[index] == "--request") { request = args[++index]; }
    else { deployment = args[++index]; }
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using var client = FoundryChatClientFactory.CreateChatClient(settings, deployment);
    var result = await HarnessRunner.RunAsync(
        client,
        request ?? "Investigate checkout latency. Inspect its health and runbook, then recommend next steps.",
        Console.WriteLine);
    Console.WriteLine("--- Final harness result ---");
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    return result.Status == "completed" ? 0 : 2;
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or OperationCanceledException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
