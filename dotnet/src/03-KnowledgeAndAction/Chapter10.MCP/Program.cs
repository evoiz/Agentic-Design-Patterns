using AgenticDesignPatterns.Shared;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Chapter10.MCP;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? selection = null;
        string? deploymentSelection = null;
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
            {
                if (deploymentSelection is not null || index + 1 >= args.Length ||
                    string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    return Usage();
                }

                deploymentSelection = args[++index];
            }
            else if (selection is null)
            {
                selection = args[index];
            }
            else
            {
                return Usage();
            }
        }

        if (selection is not null &&
            selection.ToLowerInvariant() is not ("all" or "greet" or "files" or "foundry-native"))
        {
            return Usage();
        }

        try
        {
            if (string.Equals(selection, "foundry-native", StringComparison.OrdinalIgnoreCase))
            {
                await FoundryNativeDemo.RunAsync(deploymentSelection);
                return 0;
            }

            var settings = FoundryChatClientFactory.LoadSettings();
            using IChatClient chatClient =
                FoundryChatClientFactory.CreateChatClient(settings, deploymentSelection);
            Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentSelection)}");

            if (selection is null ||
                string.Equals(selection, "all", StringComparison.OrdinalIgnoreCase))
            {
                await RunHttpGreetingDemoAsync(chatClient);
                await RunFilesystemDemoAsync(chatClient);
            }
            else if (string.Equals(selection, "greet", StringComparison.OrdinalIgnoreCase))
            {
                await RunHttpGreetingDemoAsync(chatClient);
            }
            else
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
    }

    private static async Task RunHttpGreetingDemoAsync(IChatClient chatClient)
    {
        var serverUrl = Environment.GetEnvironmentVariable("CHAPTER10_MCP_SERVER_URL")
            ?? "http://localhost:8000/mcp";
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("CHAPTER10_MCP_SERVER_URL must be an absolute HTTP or HTTPS URL.");
        }

        Console.WriteLine("\n=== HTTP MCP greeting agent ===");
        Console.WriteLine($"Server: {endpoint}");
        await using var mcpClient = await McpAgent.ConnectToHttpServerAsync(endpoint);
        var response = await McpAgent.RunAsync(
            chatClient,
            mcpClient,
            "Use the greet tool to greet Ada.",
            ["greet"]);
        Console.WriteLine("\n--- Final agent response ---");
        Console.WriteLine(response);
    }

    private static async Task RunFilesystemDemoAsync(IChatClient chatClient)
    {
        var targetDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "mcp_managed_files"));
        Directory.CreateDirectory(targetDirectory);

        Console.WriteLine("\n=== stdio MCP filesystem agent ===");
        Console.WriteLine($"Managed directory: {targetDirectory}");
        var targetFile = Path.Combine(targetDirectory, "mcp-demo.txt");
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Chapter10Filesystem",
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-filesystem", targetDirectory],
            InheritEnvironmentVariables = false,
            EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables()
        });
        await using var mcpClient = await McpClient.CreateAsync(transport);
        var response = await McpAgent.RunAsync(
            chatClient,
            mcpClient,
            $"Create or replace {targetFile} with a short greeting, then read that file and report its contents.",
            ["list_directory", "read_file", "write_file"]);
        Console.WriteLine("\n--- Final agent response ---");
        Console.WriteLine(response);
    }

    private static int Usage()
    {
        Console.Error.WriteLine(
            "Usage: dotnet run --project dotnet/src/03-KnowledgeAndAction/Chapter10.MCP " +
            "[all|greet|files|foundry-native] [--deployment <name>]");
        return 1;
    }
}
