using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.AI.AgentServer.Core;
using Azure.AI.AgentServer.Invocations;
using Chapter13.HumanInTheLoop.Hosted;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace AgenticDesignPatterns.HumanInTheLoop.Tests;

[CollectionDefinition("Hosted invocation environment", DisableParallelization = true)]
public sealed class HostedInvocationEnvironmentCollection;

[Collection("Hosted invocation environment")]
public sealed class CustomerSupportInvocationHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Pending_retry_replays_request_and_sessions_are_partitioned_by_owner()
    {
        using var chatClient = new FakeApprovalChatClient(approvalCount: 1);
        var handler = new CustomerSupportInvocationHandler(chatClient);
        var sessionId = $"owner-partition-{Guid.NewGuid():N}";
        const string firstOwner = "owner-a";
        const string secondOwner = "owner-b";
        var task = CreateTaskBody();

        try
        {
            var initial = await InvokeAsync(handler, sessionId, firstOwner, task);
            Assert.Equal(StatusCodes.Status200OK, initial.StatusCode);
            Assert.Equal("awaiting_approval", initial.Body.GetProperty("status").GetString());
            var firstRequestId = initial.Body.GetProperty("approval").GetProperty("request_id").GetString();

            var retry = await InvokeAsync(handler, sessionId, firstOwner, task);
            Assert.Equal(firstRequestId, retry.Body.GetProperty("approval").GetProperty("request_id").GetString());
            Assert.Equal(1, chatClient.ModelCallCount);

            var otherOwner = await InvokeAsync(handler, sessionId, secondOwner, task);
            Assert.Equal("awaiting_approval", otherOwner.Body.GetProperty("status").GetString());
            var otherRequestId = otherOwner.Body.GetProperty("approval").GetProperty("request_id").GetString();
            Assert.NotEqual(firstRequestId, otherRequestId);
            Assert.Equal(2, chatClient.ModelCallCount);

            var completed = await InvokeAsync(
                handler,
                sessionId,
                firstOwner,
                JsonSerializer.Serialize(new { decision = "approve", requestId = firstRequestId }, JsonOptions));
            Assert.Equal("completed", completed.Body.GetProperty("status").GetString());
        }
        finally
        {
            DeleteSessionFile(sessionId, firstOwner);
            DeleteSessionFile(sessionId, secondOwner);
        }
    }

    [Fact]
    public async Task Hosted_request_without_platform_identity_is_rejected()
    {
        using var chatClient = new FakeApprovalChatClient(approvalCount: 1);
        var handler = new CustomerSupportInvocationHandler(chatClient);
        var previous = Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT");
        Environment.SetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT", "true");

        try
        {
            var result = await InvokeAsync(
                handler,
                $"missing-identity-{Guid.NewGuid():N}",
                ownerId: null,
                CreateTaskBody());

            Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
            Assert.Equal(0, chatClient.ModelCallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT", previous);
        }
    }

    [Fact]
    public async Task Multiple_pending_approvals_are_returned_and_resolved_together()
    {
        using var chatClient = new FakeApprovalChatClient(approvalCount: 2);
        var handler = new CustomerSupportInvocationHandler(chatClient);
        var sessionId = $"multiple-approvals-{Guid.NewGuid():N}";
        const string ownerId = "multiple-approval-owner";

        try
        {
            var pending = await InvokeAsync(handler, sessionId, ownerId, CreateTaskBody());
            Assert.Equal("awaiting_approval", pending.Body.GetProperty("status").GetString());
            var approvals = pending.Body.GetProperty("approvals");
            Assert.Equal(2, approvals.GetArrayLength());

            var decisions = approvals.EnumerateArray()
                .Select(approval => new
                {
                    decision = "approve",
                    requestId = approval.GetProperty("request_id").GetString()
                })
                .ToArray();
            var completed = await InvokeAsync(
                handler,
                sessionId,
                ownerId,
                JsonSerializer.Serialize(new { approvals = decisions }, JsonOptions));

            Assert.Equal("completed", completed.Body.GetProperty("status").GetString());
            Assert.Equal(2, chatClient.FunctionResultCount);
        }
        finally
        {
            DeleteSessionFile(sessionId, ownerId);
        }
    }

    private static string CreateTaskBody() =>
        JsonSerializer.Serialize(new
        {
            task = "Escalate this complex issue.",
            customer = new { name = "Sam", tier = "premium" }
        }, JsonOptions);

    private static async Task<InvocationResult> InvokeAsync(
        CustomerSupportInvocationHandler handler,
        string sessionId,
        string? ownerId,
        string body)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.ContentType = "application/json";
        httpContext.Response.Body = new MemoryStream();
        var context = new InvocationContext(
            Guid.NewGuid().ToString("N"),
            sessionId,
            new Dictionary<string, string>(),
            new Dictionary<string, StringValues>(),
            new PlatformContext(ownerId, callId: null));

        await handler.HandleAsync(
            httpContext.Request,
            httpContext.Response,
            context,
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        return new InvocationResult(
            httpContext.Response.StatusCode,
            document.RootElement.Clone());
    }

    private static void DeleteSessionFile(string sessionId, string ownerId)
    {
        var partitionKey = $"{ownerId}\0{sessionId}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(partitionKey)))
            .ToLowerInvariant();
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".chapter13-human-in-the-loop",
            $"session-{hash}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record InvocationResult(int StatusCode, JsonElement Body);

    private sealed class FakeApprovalChatClient(int approvalCount) : IChatClient
    {
        private int _modelCallCount;
        private int _functionCallId;

        public int ModelCallCount => Volatile.Read(ref _modelCallCount);
        public int FunctionResultCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _modelCallCount);
            var results = messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .ToArray();
            if (results.Length > 0)
            {
                FunctionResultCount = results.Length;
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "Escalation decisions were processed.")));
            }

            Assert.Contains(
                "escalate_to_human",
                options?.Tools?.Select(tool => tool.Name) ?? []);
            var calls = Enumerable.Range(0, approvalCount)
                .Select(index => new FunctionCallContent(
                    $"call-{Interlocked.Increment(ref _functionCallId)}",
                    "escalate_to_human",
                    new Dictionary<string, object?> { ["issueType"] = $"issue-{index + 1}" }))
                .Cast<AIContent>()
                .ToArray();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, calls)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var message in response.Messages)
            {
                yield return new ChatResponseUpdate(message.Role, message.Text);
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
