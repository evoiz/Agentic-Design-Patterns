using System.Text.Json;
using Azure.AI.AgentServer.Invocations;
using Chapter13.HumanInTheLoop;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using static Chapter13.HumanInTheLoop.Hosted.SupportInvocationSession;

namespace Chapter13.HumanInTheLoop.Hosted;

// Invocations protocol: {"task", "customer"} starts a request; {"decision","requestId"} or {"approvals":[...]}
// resolves the pending escalation. Sessions are durable and partitioned by platform caller identity.
public sealed class CustomerSupportInvocationHandler(IChatClient chatClient) : InvocationHandler
{
    private const int MaximumTaskLength = 4000;
    private static JsonSerializerOptions JsonOptions => SupportSessionStore.JsonOptions;

    public override async Task HandleAsync(
        HttpRequest request, HttpResponse response, InvocationContext context, CancellationToken cancellationToken)
    {
        try
        {
            var body = await ReadRequestAsync(request, cancellationToken);
            var sessionId = context.SessionId;
            var ownerId = context.PlatformContext.UserIdKey;
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT")) &&
                string.IsNullOrWhiteSpace(ownerId))
            {
                throw new InvocationException(
                    StatusCodes.Status401Unauthorized, "The hosted platform did not provide an authenticated caller identity.");
            }

            var sessionLock = SupportSessionStore.GetLock(sessionId, ownerId);
            await sessionLock.WaitAsync(cancellationToken);
            try
            {
                var stored = SupportSessionStore.Load(sessionId, ownerId);
                if (stored is not null && !string.Equals(stored.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    throw new InvocationException(StatusCodes.Status404NotFound, "No session found for this caller.");
                }

                var result = body.Task is not null
                    ? await StartSupportRequestAsync(body, stored, sessionId, ownerId, cancellationToken)
                    : await ResolveApprovalAsync(body, stored, sessionId, ownerId, cancellationToken);
                await WriteSessionAsync(response, result, context.InvocationId, cancellationToken);
            }
            finally
            {
                sessionLock.Release();
            }
        }
        catch (InvocationException exception)
        {
            await WriteErrorAsync(response, exception.StatusCode, exception.Message, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            await WriteErrorAsync(response, StatusCodes.Status400BadRequest, exception.Message, cancellationToken);
        }
    }

    private static async Task<CustomerSupportInvocationRequest> ReadRequestAsync(
        HttpRequest request, CancellationToken cancellationToken)
    {
        CustomerSupportInvocationRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<CustomerSupportInvocationRequest>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            throw new ArgumentException(
                "Request body must be a JSON object containing either a task or an approval decision.");
        }

        if (body is null)
        {
            throw new ArgumentException("Request body is required.");
        }

        var hasTask = body.Task is not null;
        if (hasTask == (body.Decision is not null || body.Approvals is not null))
        {
            throw new ArgumentException("Provide exactly one of 'task' or 'decision'.");
        }

        if (body.Approvals is not null && (body.Decision is not null || body.RequestId is not null))
        {
            throw new ArgumentException(
                "Use either 'approvals' or the single-approval 'decision' and 'requestId' fields.");
        }

        if (hasTask && string.IsNullOrWhiteSpace(body.Task))
        {
            throw new ArgumentException("The task cannot be empty.");
        }

        if (body.Task?.Length > MaximumTaskLength)
        {
            throw new ArgumentException($"The task cannot exceed {MaximumTaskLength} characters.");
        }

        return body;
    }

    private async Task<SupportInvocationSession> StartSupportRequestAsync(
        CustomerSupportInvocationRequest body,
        SupportInvocationSession? previous,
        string sessionId,
        string? ownerId,
        CancellationToken cancellationToken)
    {
        var task = body.Task!;
        var customer = body.Customer ?? new SupportCustomer();
        customer.Validate();
        if (previous?.Status == AwaitingApproval)
        {
            // An identical retry replays the stored approval request instead of re-running the model.
            var isSameRequest = previous.Task == task &&
                JsonSerializer.Serialize(previous.Customer, JsonOptions) == JsonSerializer.Serialize(customer, JsonOptions);
            return isSameRequest
                ? previous
                : throw new InvocationException(
                    StatusCodes.Status409Conflict,
                    "This session has a pending approval. Resolve it before submitting another task.");
        }

        var agent = CustomerSupportAgent.Create(chatClient, customer);
        var agentSession = await agent.CreateSessionAsync(cancellationToken);
        var agentResponse = await agent.RunAsync(task, agentSession, cancellationToken: cancellationToken);
        return await SaveAsync(agent, agentSession, agentResponse, customer, task, sessionId, ownerId, cancellationToken);
    }

    private async Task<SupportInvocationSession> ResolveApprovalAsync(
        CustomerSupportInvocationRequest body,
        SupportInvocationSession? stored,
        string sessionId,
        string? ownerId,
        CancellationToken cancellationToken)
    {
        if (stored is not { Status: AwaitingApproval, PendingApprovals.Count: > 0 })
        {
            throw new InvocationException(StatusCodes.Status409Conflict, "This session has no pending approval.");
        }

        var decisions = body.Approvals ?? [new ApprovalDecision(body.RequestId, body.Decision)];
        if (decisions.Count != stored.PendingApprovals.Count)
        {
            throw new ArgumentException("Provide exactly one decision for every pending approval.");
        }

        // Each decision must consume a distinct pending request ID; duplicates or unknown IDs are rejected.
        var pendingById = stored.PendingApprovals.ToDictionary(approval => approval.RequestId, StringComparer.Ordinal);
        var approvalResponses = new List<AIContent>(decisions.Count);
        foreach (var item in decisions)
        {
            var decision = item.Decision?.Trim().ToLowerInvariant();
            if (decision is not ("approve" or "reject"))
            {
                throw new ArgumentException("Each decision must be 'approve' or 'reject'.");
            }

            if (string.IsNullOrWhiteSpace(item.RequestId) || !pendingById.Remove(item.RequestId, out var pending))
            {
                throw new ArgumentException("Each requestId must match a pending approval exactly once.");
            }

            approvalResponses.Add(pending.ToRequest().CreateResponse(decision == "approve"));
        }

        var agent = CustomerSupportAgent.Create(chatClient, stored.Customer);
        var agentSession = await agent.DeserializeSessionAsync(stored.AgentSession, cancellationToken: cancellationToken);
        var agentResponse = await agent.RunAsync(
            new ChatMessage(ChatRole.User, approvalResponses), agentSession, cancellationToken: cancellationToken);
        return await SaveAsync(
            agent, agentSession, agentResponse, stored.Customer, stored.Task, sessionId, ownerId, cancellationToken);
    }

    private static async Task<SupportInvocationSession> SaveAsync(
        AIAgent agent,
        AgentSession agentSession,
        AgentResponse agentResponse,
        SupportCustomer customer,
        string task,
        string sessionId,
        string? ownerId,
        CancellationToken cancellationToken)
    {
        var pendingApprovals = agentResponse.Messages
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>()
            .Select(PendingApproval.From)
            .ToList();
        if (pendingApprovals.Count == 0 && string.IsNullOrWhiteSpace(agentResponse.Text))
        {
            throw new InvalidOperationException("The support agent returned an empty response.");
        }

        var session = new SupportInvocationSession(
            sessionId,
            ownerId,
            pendingApprovals.Count > 0 ? AwaitingApproval : Completed,
            task,
            customer,
            await agent.SerializeSessionAsync(agentSession, cancellationToken: cancellationToken),
            pendingApprovals,
            agentResponse.Text,
            agentResponse.Text);
        SupportSessionStore.Save(session);
        return session;
    }

    private static Task WriteSessionAsync(
        HttpResponse response, SupportInvocationSession session, string invocationId, CancellationToken cancellationToken)
    {
        if (session.Status != AwaitingApproval)
        {
            return response.WriteAsJsonAsync(
                new
                {
                    session_id = session.SessionId,
                    invocation_id = invocationId,
                    status = session.Status,
                    final_output = session.AssistantMessage
                },
                JsonOptions,
                cancellationToken);
        }

        var approvals = session.PendingApprovals!
            .Select(pending => new { request_id = pending.RequestId, tool_name = pending.Name, arguments = pending.Arguments })
            .ToArray();
        // One pending approval is returned as "approval" for simple clients; several as "approvals".
        object payload = approvals.Length == 1
            ? new
            {
                session_id = session.SessionId,
                invocation_id = invocationId,
                status = AwaitingApproval,
                assistant_message = session.AssistantMessage,
                approval = approvals[0]
            }
            : new
            {
                session_id = session.SessionId,
                invocation_id = invocationId,
                status = AwaitingApproval,
                assistant_message = session.AssistantMessage,
                approvals
            };
        return response.WriteAsJsonAsync(payload, payload.GetType(), JsonOptions, cancellationToken);
    }

    private static Task WriteErrorAsync(HttpResponse response, int statusCode, string message, CancellationToken cancellationToken)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(new { error = message }, JsonOptions, cancellationToken);
    }

    public sealed record CustomerSupportInvocationRequest
    {
        public string? Task { get; init; }
        public SupportCustomer? Customer { get; init; }
        public string? Decision { get; init; }
        public string? RequestId { get; init; }
        public List<ApprovalDecision>? Approvals { get; init; }
    }

    public sealed record ApprovalDecision(string? RequestId, string? Decision);

    private sealed class InvocationException(int statusCode, string message) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }
}
