using System.Net;
using System.Text;
using System.Text.Json;
using Azure.AI.AgentServer.Core;
using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Azure.Core;
using Chapter07.MultiAgent;
using Chapter07.MultiAgent.Distributed;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.MultiAgent.Tests;

public sealed class DistributedCrewTests
{
#pragma warning disable MAAI001
    [Fact]
    public async Task Stateless_host_partitions_by_response_instead_of_missing_user_identity()
    {
        var provider = new Chapter07.MultiAgent.Hosted.RequestIsolationKeyProvider();
        var first = await provider.GetKeysAsync(new TestResponseContext("response-1"), new CreateResponse());
        var second = await provider.GetKeysAsync(new TestResponseContext("response-2"), new CreateResponse());

        Assert.Equal("request:response-1", first!.UserId);
        Assert.Equal("request:response-2", second!.UserId);
        Assert.NotEqual(first.UserId, second.UserId);
    }
#pragma warning restore MAAI001

    private const string Completed = """
        {"status":"completed","output":[{"type":"message","role":"assistant","content":[
          {"type":"output_text","text":"specialist result"}
        ]}]}
        """;

    [Fact]
    public async Task Remote_call_uses_the_exact_endpoint_and_managed_identity_token()
    {
        var credential = new RecordingCredential();
        using var handler = new RecordingHandler(Completed);
        using var http = new HttpClient(handler);
        var specialist = new RemoteSpecialist(http,
            "https://demo.services.ai.azure.com/api/projects/test/agents/researcher/endpoint/protocols/openai/responses?api-version=v1",
            credential);

        Assert.Equal("specialist result", await specialist.InvokeAsync("caller topic"));
        Assert.Equal("https://ai.azure.com/.default", Assert.Single(credential.Scopes!));
        Assert.Equal("Bearer demo-token", handler.Authorization);
        Assert.Contains("?api-version=v1", handler.Uri!.AbsoluteUri);
        Assert.Null(handler.UserIdentity);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("caller topic", body.RootElement.GetProperty("input").GetString());
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("previous_response_id", out _));
        Assert.False(body.RootElement.TryGetProperty("conversation", out _));
    }

    [Fact]
    public async Task Platform_headers_preserve_call_id_without_impersonating_a_user()
    {
        using var handler = new RecordingHandler(Completed);
        using var http = new HttpClient(new FoundryCallIdHandler(handler));
        var specialist = new RemoteSpecialist(http,
            "https://demo.services.ai.azure.com/api/projects/test/agents/researcher/endpoint/protocols/openai/responses?api-version=v1",
            new RecordingCredential());

        var context = new FoundryAgentRequestContext
        {
            CallId = "platform-call-id",
            UserId = "platform-user-id"
        };
        var header = Assert.Single(context.PlatformHeaders());
        Assert.Equal("x-agent-foundry-call-id", header.Key);
        Assert.Equal("platform-call-id", header.Value);

        await specialist.InvokeAsync("local development without ambient context");
        Assert.Null(handler.CallId);
        Assert.Null(handler.UserIdentity);
        Assert.Null(handler.PlatformUserId);
    }

    [Fact]
    public async Task Localhost_calls_do_not_send_an_Azure_token()
    {
        var credential = new RecordingCredential();
        using var handler = new RecordingHandler(Completed);
        using var http = new HttpClient(handler);
        var specialist = new RemoteSpecialist(http, "http://127.0.0.1:8091/responses", credential);

        await specialist.InvokeAsync("test");

        Assert.Null(handler.Authorization);
        Assert.Null(handler.UserIdentity);
        Assert.Null(credential.Scopes);
    }

    [Theory]
    [InlineData("http://example.com/responses")]
    [InlineData("https://example.com/responses")]
    [InlineData("https://demo.services.ai.azure.com/other")]
    [InlineData("https://user:pass@demo.services.ai.azure.com/responses")]
    public void Non_Foundry_or_incomplete_endpoints_are_rejected(string endpoint)
    {
        using var http = new HttpClient();
        Assert.Throws<ArgumentException>(() => new RemoteSpecialist(http, endpoint, new RecordingCredential()));
    }

    [Theory]
    [InlineData("""{"status":"failed","output":[]}""")]
    [InlineData("""{"status":"incomplete","output":[]}""")]
    [InlineData("""{"status":"queued","output":[]}""")]
    [InlineData("""{"status":"completed","output":[]}""")]
    [InlineData("""{"status":"completed"}""")]
    public async Task Nonfinal_or_empty_specialist_responses_fail_explicitly(string response)
    {
        using var handler = new RecordingHandler(response);
        using var http = new HttpClient(handler);
        var specialist = new RemoteSpecialist(http, "http://127.0.0.1:8091/responses", new RecordingCredential());
        await Assert.ThrowsAsync<InvalidOperationException>(() => specialist.InvokeAsync("test"));
    }

    [Fact]
    public async Task Http_failure_is_not_replaced_with_a_model_fallback()
    {
        using var handler = new RecordingHandler("Forbidden", HttpStatusCode.Forbidden);
        using var http = new HttpClient(handler);
        var specialist = new RemoteSpecialist(http, "http://127.0.0.1:8091/responses", new RecordingCredential());
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => specialist.InvokeAsync("test"));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task Research_is_passed_to_the_remote_writer_with_the_original_request()
    {
        var requests = new List<string>();
        var result = await MultiAgentWorkflow.RunCrewAsync(
            (input, _) =>
            {
                requests.Add(input);
                return Task.FromResult("research evidence");
            },
            (input, _) =>
            {
                requests.Add(input);
                return Task.FromResult("completed article");
            },
            "caller topic");

        Assert.Equal("completed article", result);
        Assert.Equal("caller topic", requests[0]);
        Assert.Contains("caller topic", requests[1]);
        Assert.Contains("research evidence", requests[1]);
    }

    [Fact]
    public async Task Research_failure_prevents_invoking_the_writer()
    {
        var writerCalled = false;
        await Assert.ThrowsAsync<HttpRequestException>(() => MultiAgentWorkflow.RunCrewAsync(
            (_, _) => throw new HttpRequestException("specialist unavailable"),
            (_, _) =>
            {
                writerCalled = true;
                return Task.FromResult("must not happen");
            },
            "topic"));
        Assert.False(writerCalled);
    }

    [Fact]
    public async Task Precancelled_request_does_not_invoke_any_specialists()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MultiAgentWorkflow.RunCrewAsync(
            (_, _) => throw new InvalidOperationException("must not happen"),
            (_, _) => throw new InvalidOperationException("must not happen"),
            "topic",
            cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task Host_passes_only_the_current_user_input_to_a_fresh_run()
    {
        var inputs = new List<string>();
        var agent = new StatelessRequestAgent("test", (input, _) =>
        {
            inputs.Add(input);
            return Task.FromResult($"result for {input}");
        });
        var response = await agent.RunAsync([
            new ChatMessage(ChatRole.User, "old task"),
            new ChatMessage(ChatRole.Assistant, "old output"),
            new ChatMessage(ChatRole.User, "new task")
        ]);

        Assert.Equal("new task", Assert.Single(inputs));
        Assert.Equal("result for new task", response.Text);
    }

    private sealed class RecordingCredential : TokenCredential
    {
        public string[]? Scopes { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Scopes = requestContext.Scopes;
            return new AccessToken("demo-token", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class TestResponseContext(string responseId) : ResponseContext(responseId);

    private sealed class RecordingHandler(
        string response,
        HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }
        public string? UserIdentity { get; private set; }
        public string? CallId { get; private set; }
        public string? PlatformUserId { get; private set; }
        public Uri? Uri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            UserIdentity = request.Headers.TryGetValues("x-ms-user-identity", out var identities)
                ? Assert.Single(identities)
                : null;
            CallId = request.Headers.TryGetValues("x-agent-foundry-call-id", out var callIds)
                ? Assert.Single(callIds)
                : null;
            PlatformUserId = request.Headers.TryGetValues("x-agent-user-id", out var userIds)
                ? Assert.Single(userIds)
                : null;
            Uri = request.RequestUri;
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
