using AgenticDesignPatterns.TestSupport;
using MCP;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.Text.Json;
using Xunit;

namespace MCP.Tests;

public sealed class HttpMcpContractTests
{
    [Fact]
    public async Task Http_client_discovers_and_invokes_only_the_greeting_tool()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<GreetingTools>();
        await using var app = builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync();

        try
        {
            var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
                .Addresses.Single();
            var endpoint = new Uri(new Uri(address), "/mcp");
            using var model = new ScriptedChatClient((messages, options) =>
            {
                var functionResult = messages.SelectMany(message => message.Contents)
                    .OfType<FunctionResultContent>().SingleOrDefault();
                if (functionResult is null)
                {
                    var greet = Assert.Single(options!.Tools!);
                    Assert.Equal("greet", greet.Name);
                    return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            "greet-1",
                            "greet",
                            JsonSerializer.Deserialize<Dictionary<string, object?>>("""{"name":"Ada"}"""))
                    ]));
                }

                Assert.Contains("Hello, Ada!", functionResult.Result?.ToString());
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello, Ada! Nice to meet you."));
            });

            await using var client = await McpAgent.ConnectToHttpServerAsync(endpoint);
            var response = await McpAgent.RunAsync(
                model,
                client,
                "Use the greeting tool for Ada.",
                ["greet"]);

            Assert.Equal("Hello, Ada! Nice to meet you.", response);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Http_client_reports_an_unavailable_server()
    {
        var endpoint = new Uri("http://127.0.0.1:1/mcp");

        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => McpAgent.ConnectToHttpServerAsync(endpoint));
    }

    [Fact]
    public async Task Http_client_honors_pre_cancelled_connection()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => McpAgent.ConnectToHttpServerAsync(new Uri("http://127.0.0.1:8000/mcp"), cancellation.Token));
    }
}
