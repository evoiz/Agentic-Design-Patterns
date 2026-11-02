using AgenticDesignPatterns.TestSupport;
using Chapter12.ExceptionHandling;
using Chapter12.ExceptionHandling.Hosted;
using Microsoft.Extensions.AI;
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
    public async Task Successful_primary_lookup_returns_precise_explanation_without_fallback()
    {
        var model = new RecordingModel("Precise record.");
        var output = new List<string>();

        var result = await ExceptionHandlingWorkflow.RunAsync(
            model.Client, "1600 Amphitheatre Parkway, Mountain View, CA", output.Add);

        Assert.Equal("Precise lookup succeeded after 1 attempt(s).\nPrecise record.", result);
        Assert.Single(model.Requests);
        Assert.Contains("37.4220, -122.0841", model.Requests[0]);
        Assert.DoesNotContain(output, line => line.Contains("General-area fallback", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Transient_lookup_failure_retries_primary_activity_only()
    {
        var model = new RecordingModel("Precise record.");
        var output = new List<string>();

        var result = await ExceptionHandlingWorkflow.RunAsync(
            model.Client, "transient: 1600 Amphitheatre Parkway, Mountain View, CA", output.Add);

        Assert.Equal("Precise lookup succeeded after 2 attempt(s).\nPrecise record.", result);
        Assert.Single(model.Requests);
        Assert.Contains(output, line => line.Contains("attempt 1 failed; next: retry primary", StringComparison.Ordinal));
        Assert.Contains(output, line => line.Contains("succeeded on attempt 2", StringComparison.Ordinal));
        Assert.DoesNotContain(output, line => line.Contains("General-area fallback", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Exhausted_primary_attempts_use_city_fallback_and_preserve_failure_context()
    {
        var model = new RecordingModel("San Francisco area result.");
        var output = new List<string>();

        var result = await ExceptionHandlingWorkflow.RunAsync(
            model.Client, "primary-unavailable: 1 Market Street, San Francisco, CA", output.Add);

        Assert.StartsWith("Precise lookup failed after 2 attempt(s)", result);
        Assert.EndsWith("San Francisco area result.", result);
        Assert.Single(model.Requests);
        Assert.Contains("Primary lookup failure:", model.Requests[0]);
        Assert.Contains("The Ferry Building", model.Requests[0]);
        Assert.Contains(output, line => line.Contains("attempt 2 failed; next: area fallback", StringComparison.Ordinal));
        Assert.Contains(output, line => line.Contains("Fallback result recorded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Model_failure_is_not_treated_as_a_recoverable_lookup_failure()
    {
        var model = new RecordingModel(" ");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExceptionHandlingWorkflow.RunAsync(model.Client, "1600 Amphitheatre Parkway, Mountain View, CA"));

        Assert.Contains("PrimaryLookup", exception.Message);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task Responses_protocol_returns_completed_recovery_result()
    {
        var model = new RecordingModel("Hosted fallback response.");
        await using var host = await HostedResponses.StartAsync(new ExceptionHandlingAgent(model.Client));

        using var body = await host.InvokeAsync(
            "primary-unavailable: 1 Market Street, San Francisco, CA", "chapter12-exception-handling");

        Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
        Assert.Contains("Hosted fallback response.", HostedResponses.OutputText(body.RootElement));
    }

    private sealed class RecordingModel
    {
        public RecordingModel(string response) =>
            Client = new ScriptedChatClient((messages, _) =>
            {
                Requests.Add(messages.Last(message => message.Role == ChatRole.User).Text);
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
            });

        public IChatClient Client { get; }
        public List<string> Requests { get; } = [];
    }
}
