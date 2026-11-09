using AgenticDesignPatterns.Shared;
using MCP;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

// Usage: [greet|all|files|foundry-native] [--deployment <name>] (default: greet)
string? selection = null;
string? deployment = null;
for (var index = 0; index < args.Length; index++)
{
    if (args[index].Equals("--deployment", StringComparison.OrdinalIgnoreCase) &&
        deployment is null && index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]))
    {
        deployment = args[++index];
    }
    else if (selection is null && args[index].ToLowerInvariant() is "all" or "greet" or "files" or "foundry-native")
    {
        selection = args[index].ToLowerInvariant();
    }
    else
    {
        Console.Error.WriteLine(
            "Usage: dotnet run --project dotnet/src/MCP [all|greet|files|foundry-native] [--deployment <name>]");
        return 1;
    }
}

selection ??= "greet";
try
{
    if (selection == "foundry-native")
    {
        await FoundryNativeDemo.RunAsync(deployment);
        return 0;
    }

    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deployment);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deployment)}");
    if (selection is "all" or "greet")
    {
        await RunHttpGreetingDemoAsync(chatClient);
    }

    if (selection is "all" or "files")
    {
        await RunFilesystemDemoAsync(chatClient);
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;

// Streamable HTTP transport to the separately running MCP.Server.
static async Task RunHttpGreetingDemoAsync(IChatClient chatClient)
{
    var serverUrl = Environment.GetEnvironmentVariable("CHAPTER10_MCP_SERVER_URL") ?? "http://localhost:8000/mcp";
    if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var endpoint))
    {
        throw new InvalidOperationException("CHAPTER10_MCP_SERVER_URL must be an absolute HTTP or HTTPS URL.");
    }

    Console.WriteLine($"\n=== HTTP MCP greeting agent ===\nServer: {endpoint}");
    await using var mcpClient = await McpAgent.ConnectToHttpServerAsync(endpoint);
    var response = await McpAgent.RunAsync(chatClient, mcpClient, "Use the greet tool to greet Ada.", ["greet"]);
    Console.WriteLine($"\n--- Final agent response ---\n{response}");
}

// stdio transport: a child filesystem server restricted to one demo directory and three tools.
static async Task RunFilesystemDemoAsync(IChatClient chatClient)
{
    var targetDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "mcp_managed_files"));
    Directory.CreateDirectory(targetDirectory);
    Console.WriteLine($"\n=== stdio MCP filesystem agent ===\nManaged directory: {targetDirectory}");
    var targetFile = Path.Combine(targetDirectory, "mcp-demo.txt");
    await using var mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "Chapter10Filesystem",
        Command = "npx",
        Arguments = ["-y", "@modelcontextprotocol/server-filesystem", targetDirectory],
        InheritEnvironmentVariables = false,
        EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables()
    }));
    var response = await McpAgent.RunAsync(
        chatClient,
        mcpClient,
        $"Create or replace {targetFile} with a short greeting, then read that file and report its contents.",
        ["list_directory", "read_file", "write_file"]);
    Console.WriteLine($"\n--- Final agent response ---\n{response}");
}
