using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace AgenticDesignPatterns.TestSupport;

public sealed class HostedResponses : IAsyncDisposable
{
    private readonly WebApplication _app;
    public HttpClient Client { get; }

    private HostedResponses(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public static async Task<HostedResponses> StartAsync(AIAgent agent)
    {
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.Services.AddFoundryResponses(agent);
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        var app = builder.Build().App;
        try
        {
            await app.StartAsync();
            return new HostedResponses(app);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public async Task<JsonDocument> InvokeAsync(string input, string model)
    {
        using var response = await Client.PostAsJsonAsync("/responses", new
        {
            model, input, stream = false, store = false
        });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    public Task<HttpResponseMessage> InvokeStreamingAsync(string input, string model) =>
        Client.PostAsJsonAsync("/responses", new
        {
            model, input, stream = true, store = false
        });

    public static string OutputText(JsonElement body) => string.Concat(
        body.GetProperty("output").EnumerateArray()
            .SelectMany(item => item.GetProperty("content").EnumerateArray())
            .Where(item => item.GetProperty("type").GetString() == "output_text")
            .Select(item => item.GetProperty("text").GetString()));

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
