using System.Text.Json;
using AgenticDesignPatterns.TestSupport;
using Chapter02.Routing;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.Routing.Tests;

public sealed class RoutingAgentTests
{
    [Theory]
    [InlineData("Booker", "Book me a hotel in Paris.", "Booking simulated.")]
    [InlineData("Info", "What is the highest mountain?", "Mount Everest.")]
    public async Task Specialist_receives_original_request_and_returns_only_final_output(
        string decision, string request, string answer)
    {
        var requests = new List<ChatMessage[]>();
        using var client = CreateClient(requests, Decision(decision), answer);
        var response = await new RoutingAgent(client).RunAsync(request);
        Assert.Equal(answer, response.Text);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, messages =>
            Assert.Equal(request, messages.Last(message => message.Role == ChatRole.User).Text));
    }

    [Fact]
    public async Task Unclear_route_does_not_call_specialist()
    {
        var requests = new List<ChatMessage[]>();
        using var client = CreateClient(requests, Decision("Unclear"));
        var response = await new RoutingAgent(client).RunAsync("Can you help me with this?");
        Assert.Contains("Please clarify", response.Text);
        Assert.Single(requests);
    }

    [Theory]
    [InlineData("unexpected token")]
    [InlineData("{}")]
    [InlineData("""{"decision":0,"rationale":"Numeric enum is not the route contract."}""")]
    [InlineData("""{"decision":"0","rationale":"Numeric enum string is not the route contract."}""")]
    [InlineData("""{"decision":99,"rationale":"Invalid route."}""")]
    [InlineData("""{"decision":"Info","rationale":""}""")]
    public async Task Invalid_structured_routes_fail_without_calling_a_specialist(string decision)
    {
        var requests = new List<ChatMessage[]>();
        using var client = CreateClient(requests, decision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RoutingAgent(client).RunAsync("A request."));
        Assert.Single(requests);
    }

    [Fact]
    public async Task Latest_user_message_is_routed_without_previous_request_state()
    {
        var requests = new List<ChatMessage[]>();
        using var client = CreateClient(requests, Decision("Info"), "First answer.", Decision("Booker"), "Booking simulated.");
        var agent = new RoutingAgent(client);
        var session = await agent.CreateSessionAsync();
        await agent.RunAsync("A general question", session);
        var response = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, "Old question"),
             new ChatMessage(ChatRole.Assistant, "Old answer"),
             new ChatMessage(ChatRole.User, "Book a flight to Tokyo")], session);

        Assert.Equal("Booking simulated.", response.Text);
        Assert.All(requests.Skip(2), messages =>
            Assert.Equal("Book a flight to Tokyo", messages.Last(message => message.Role == ChatRole.User).Text));
    }

    [Fact]
    public async Task Missing_input_and_cancellation_do_not_call_the_model()
    {
        var requests = new List<ChatMessage[]>();
        using var client = CreateClient(requests);
        var agent = new RoutingAgent(client);
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            agent.RunAsync([new ChatMessage(ChatRole.Assistant, "Not a request")]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            agent.RunAsync("A question", cancellationToken: cancellation.Token));
        Assert.Empty(requests);
    }

    [Fact]
    public async Task Model_failure_is_not_returned_as_success()
    {
        using var client = CreateClient([]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new RoutingAgent(client).RunAsync("A question"));
        Assert.Contains("failed", error.Message);
    }

    [Fact]
    public async Task Hosted_smoke_returns_the_selected_specialists_answer()
    {
        using var client = CreateClient([], Decision("Booker"), "Booking simulated.");
        await using var host = await HostedResponses.StartAsync(new RoutingAgent(client));
        using var response = await host.InvokeAsync("Book me a hotel in Paris.", "chapter02-routing");
        Assert.Equal("completed", response.RootElement.GetProperty("status").GetString());
        Assert.Equal("Booking simulated.", HostedResponses.OutputText(response.RootElement));
    }

    private static string Decision(string decision) => JsonSerializer.Serialize(new { decision, rationale = "Test decision." });

    private static ScriptedChatClient CreateClient(List<ChatMessage[]> requests, params string[] responses)
    {
        var queue = new Queue<string>(responses);
        return new ScriptedChatClient((messages, options) =>
        {
            requests.Add(messages.ToArray());
            if (requests.Count == 1)
            {
                Assert.NotNull(options?.ResponseFormat);
            }

            return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                queue.TryDequeue(out var response) ? response : throw new InvalidOperationException("Scripted model response is missing.")));
        });
    }
}
