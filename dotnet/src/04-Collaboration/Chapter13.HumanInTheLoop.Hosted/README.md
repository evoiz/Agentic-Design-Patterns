# Chapter 13 — Hosted Human-in-the-Loop Support Agent

Hosts the [Chapter 13 support agent](../Chapter13.HumanInTheLoop/README.md) through Foundry's **Invocations protocol**, preserving the approval pause across requests. Common build, deploy, inference RBAC, and cleanup steps are in the [hosting guide](../../../HOSTING.md).

## Where to look

- `CustomerSupportInvocationHandler.HandleAsync` — request validation, caller identity check, per-session lock.
- `StartSupportRequestAsync` / `ResolveApprovalAsync` — start, idempotent retry, and approval resumption.
- `SupportSessionStore` — owner-partitioned, atomically written session files; `PendingApproval.ToRequest` restores approval requests.

## Request contract

Start a request:

```json
{
  "task": "My headphones still disconnect after troubleshooting. Escalate this complex issue.",
  "customer": { "name": "Sam", "tier": "premium", "recentPurchases": ["Wireless headphones"], "supportHistory": [] }
}
```

The response is `status: "completed"` with `final_output`, or `status: "awaiting_approval"` with `approval` (one) or `approvals` (several), each containing `request_id`, `tool_name`, and `arguments`.

Resolve in the same `agent_session_id` with `{"decision":"approve","requestId":"<id>"}` (or `"reject"`), or for several:

```json
{ "approvals": [ { "decision": "approve", "requestId": "<id-1>" }, { "decision": "reject", "requestId": "<id-2>" } ] }
```

Rules enforced by the handler:

- Exactly one of `task` or a decision; task ≤ 4,000 characters; customer profile fields and lists are bounded.
- Every pending request ID must be answered exactly once; stale, duplicate, or unknown IDs return 400; a decision with nothing pending (including a replay after completion) returns 409.
- Retrying the identical task while approval is pending replays the saved proposal without a model call; a different task returns 409.
- Each platform user gets an isolated partition (storage path and lock). In Foundry, a request without the platform user identity returns 401; locally the session ID is the caller boundary.

The file store and in-process lock are a single-instance sample design. For multiple replicas, use encrypted shared persistence, distributed locking, and retention policies.

## Authentication

**Identity-only:** the host always uses `DefaultAzureCredential` (developer identity locally, managed identity in Azure) and ignores any configured API key. The runtime identity needs **Cognitive Services OpenAI User** on the model-hosting account — see the hosting guide.

## Run locally

```powershell
$env:Foundry__ProjectEndpoint = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = "<model-deployment>"
dotnet run --project .\dotnet\src\04-Collaboration\Chapter13.HumanInTheLoop.Hosted
```

```powershell
$sessionId = "support-demo-001"
$first = Invoke-RestMethod -Method Post -Uri "http://localhost:8088/invocations?agent_session_id=$sessionId" `
  -ContentType "application/json" `
  -Body '{"task":"My headphones still disconnect after troubleshooting. Escalate this complex issue.","customer":{"name":"Sam","tier":"premium","recentPurchases":["Wireless headphones"],"supportHistory":[]}}'
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/invocations?agent_session_id=$sessionId" `
  -ContentType "application/json" -Body (@{ decision = "approve"; requestId = $first.approval.request_id } | ConvertTo-Json)
```

## Deploy and invoke

```powershell
azd deploy chapter13-human-in-the-loop --no-prompt
azd ai agent invoke chapter13-human-in-the-loop --protocol invocations --session-id support-demo -f .\support-request.json
azd ai agent invoke chapter13-human-in-the-loop --protocol invocations --session-id support-demo -f .\approval.json
```

`support-request.json` holds the start body above; `approval.json` holds the decision with the returned `request_id`.

## Telemetry

Model calls capture sensitive data (customer prompts, tool arguments) under `Chapter13.HumanInTheLoop`. Use synthetic data and restrict telemetry access.
