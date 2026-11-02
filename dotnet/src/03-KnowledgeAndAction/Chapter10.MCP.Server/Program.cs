using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<GreetingTools>();

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();

[McpServerToolType]
public sealed class GreetingTools
{
    [McpServerTool(Name = "greet")]
    [Description("Generate a friendly greeting for a person by name.")]
    public static string Greet(
        [Description("The name of the person to greet.")] string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return $"Hello, {name.Trim()}! Nice to meet you.";
    }
}
