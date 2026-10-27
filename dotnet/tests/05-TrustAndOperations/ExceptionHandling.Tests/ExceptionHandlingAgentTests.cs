using System.Net.Http.Json;
using System.Text.Json;
using Azure.AI.AgentServer.Core;
using Chapter12.ExceptionHandling;
using Chapter12.ExceptionHandling.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using Xunit;

namespace AgenticDesignPatterns.ExceptionHandling.Tests;

public sealed class ExceptionHandlingAgentTests
{
    [Theory]
    [InlineData("1 Market Street, San Francisco", "San Francisco")]
    [InlineData("1 Market Street, San Francisco, CA, USA", "San Francisco")]
    public void City_extraction_prefers_supported_city_components(string address, string expectedCity)
    {
        Assert.Equal(expectedCity, LocationLookupService.ExtractCity(address));
    }

    [Fact]
    public async Task Transient_lookup_failure_retries_primary_activity_only()
    {
        using var client = new ScriptedChatClient("Precise record.", "Final precise response.");
        var checkpoints = new List<string>();

        var result = await ExceptionHandlingWorkflow.RunAsync(
            client,
            "transient: 1600 Amphitheatre Parkway, Mountain View, CA",
            checkpoints.Add);

        Assert.Equal("Final precise response.", result);
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains(checkpoints, line => line.Contains("attempt 1 failed", StringComparison.Ordinal));
        Assert.Contains(checkpoints, line => line.Contains("succeeded on attempt 2", StringComparison.Ordinal));
        Assert.DoesNotContain(checkpoints, line => line.Contains("General-area fallback", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Exhausted_primary_attempts_use_city_fallback_and_preserve_failure_context()
    {
        using var client = new ScriptedChatClient("San Francisco area result.", "Final fallback response.");
        var checkpoints = new List<string>();

        var result = await ExceptionHandlingWorkflow.RunAsync(
            client,
            "primary-unavailable: 1 Market Street, San Francisco, CA",
            checkpoints.Add);

        Assert.Equal("Final fallback response.", result);
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains(
            "Primary lookup failure:",
            client.Requests[0].Last(message => message.Role == ChatRole.User).Text);
        Assert.Contains(
            "San Francisco",
            client.Requests[0].Last(message => message.Role == ChatRole.User).Text);
        Assert.Contains(checkpoints, line => line.Contains("attempt 2 failed", StringComparison.Ordinal));
        Assert.Contains(checkpoints, line => line.Contains("fallback result recorded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Responses_protocol_returns_completed_recovery_result()
    {
        using var model = new ScriptedChatClient("San Francisco area result.", "Final hosted fallback response.");
        using var client = model.AsBuilder()
            .UseOpenTelemetry(
                sourceName: ExceptionHandlingWorkflow.TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.ConfigureTracing(tracing => tracing.AddSource(ExceptionHandlingWorkflow.TelemetrySourceName));
        builder.Services.AddFoundryResponses(new ExceptionHandlingAgent(client));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();
        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter12-exception-handling",
                input = "primary-unavailable: 1 Market Street, San Francisco, CA",
                stream = false,
                store = false
            });
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
            var text = body.RootElement.GetProperty("output").EnumerateArray()
                .SelectMany(item => item.GetProperty("content").EnumerateArray())
                .Where(item => item.GetProperty("type").GetString() == "output_text")
                .Select(item => item.GetProperty("text").GetString());
            Assert.Contains("Final hosted fallback response.", string.Concat(text));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class ScriptedChatClient(params string[] responses) : IChatClient
    {
        private readonly Queue<string> _responses = new(responses);
        public List<List<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(messages.ToList());
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("Scripted model response is missing.");
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Chapter 12 model calls should be non-streaming.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
