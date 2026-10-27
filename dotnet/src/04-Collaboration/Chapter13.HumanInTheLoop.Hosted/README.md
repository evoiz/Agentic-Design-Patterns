# Chapter 13 — Hosted Human-in-the-Loop Support Agent

Expose the [Chapter 13 MAF customer-support agent](../Chapter13.HumanInTheLoop/README.md) through Foundry's **Invocations protocol**. It preserves the approval pause: an escalation request returns a pending approval, and the caller resumes the same session with an approval or rejection decision. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter13-human-in-the-loop` service.

## What it does

Start with an invocation JSON object containing a non-empty `task` (at most 4,000 characters) and optional `customer` context. `customer` has `name`, `tier`, `recentPurchases`, and `supportHistory`; defaults are `valued customer`, `standard`, and empty lists. Names are limited to 120 characters, tiers to 80, each list to 10 items, and each item to 200 characters. The agent can call deterministic sample tools for troubleshooting and ticket creation. The `escalate_to_human` tool requires explicit approval; the ticket and escalation are simulations, not external support-system actions.

A task response is either `status: "completed"` with `final_output`, or `status: "awaiting_approval"` with an `approval` (one request) or `approvals` (multiple requests). Each approval contains `request_id`, `tool_name`, and `arguments`. For one pending request, submit `decision` (`approve` or `reject`) and its exact `requestId`; for multiple requests, submit an `approvals` array with one decision per request. The decision invocation must use the same `agent_session_id`. Every pending ID must be supplied exactly once; stale, missing, or mismatched IDs are rejected. The request body must contain exactly one of `task` or approval decision(s). A decision with no pending approval and a new task while approvals remain pending return conflicts.

If a task retry arrives in a session with a pending approval and has identical task and customer context, the host replays the saved proposal without rerunning the model, allowing recovery if the first response was lost. A different task conflicts until pending approvals are resolved. Each Foundry platform user has an isolated session partition, including its storage path and in-process lock; hosted invocations without the platform-injected user identity are rejected. Locally, when platform identity is absent, the protocol session ID is the caller boundary. Serialized MAF session state and pending approvals are atomically written under the host user's home directory, allowing a later invocation to resume after process restart. The file store and lock are a single-instance sample design, not distributed persistence; production deployments need encrypted shared persistence, distributed locking, retention/deletion policies, and protection for customer/profile data.

## Try it: example prompts

Invocations use JSON task and decision requests rather than a plain-text Responses prompt. Use the same `agent_session_id` for each step of an approval round trip.

| Prompt | What happens | Expected result |
|---|---|---|
| `{"task":"My wireless headphones keep disconnecting. What should I try?","customer":{"name":"Sam","tier":"premium","recentPurchases":["Wireless headphones"],"supportHistory":[]}}` | Starts a normal support task; the agent can use the troubleshooting tool. | A `completed` response with `final_output`, or an `awaiting_approval` response if the agent judges escalation appropriate. |
| `{"task":"My headphones still disconnect after troubleshooting. Escalate this complex issue.","customer":{"name":"Sam","tier":"premium","recentPurchases":["Wireless headphones"],"supportHistory":["Previously contacted support about Bluetooth pairing."]}}` | Starts an escalation scenario and may request the approval-protected escalation tool. | If escalation is requested, `status` is `awaiting_approval` and the response contains an `approval` or `approvals` object with request IDs and tool arguments. |
| `{"decision":"approve","requestId":"<request_id from the pending response>"}` | Resumes that same session and approves the matching pending tool request. | The agent continues after approval and returns a completed `final_output`; approval is not implied before this decision. |
| `{"decision":"reject","requestId":"<request_id from the pending response>"}` | Resumes the same session but denies the matching pending request. | The agent continues without executing that escalation; the response reflects the remaining support flow. |
| `{"decision":"approve","requestId":"stale-id"}` | Attempts to approve an ID that is not pending in this session. | The request is rejected; each pending `requestId` must match exactly once. |

## Run locally

Prerequisites: .NET 10 SDK, PowerShell 7, Azure CLI, azd with the `azure.ai.agents` extension, and local Entra ID access to a Foundry project/model. The host uses managed identity in Azure and does not require the console project's API key.

From the repository root:

```powershell
Set-Location .\dotnet\src\04-Collaboration\Chapter13.HumanInTheLoop.Hosted
$env:Foundry__ProjectEndpoint = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = '<model-deployment>'
azd ai agent run chapter13-human-in-the-loop
# or, without azd:
dotnet run
```

The server listens on port 8088 by default; keep it private. In another PowerShell terminal, from the same folder, use the same session ID for the approval round trip:

```powershell
$sessionId = 'support-demo-001'
$first = Invoke-RestMethod -Method Post `
  -Uri "http://localhost:8088/invocations?agent_session_id=$sessionId" `
  -ContentType 'application/json' `
  -Body '{"task":"My headphones still disconnect after troubleshooting. Escalate this complex issue.","customer":{"name":"Sam","tier":"premium","recentPurchases":["Wireless headphones"],"supportHistory":[]}}'
$first

$requestId = $first.approval.request_id
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:8088/invocations?agent_session_id=$sessionId" `
  -ContentType 'application/json' `
  -Body (@{ decision = 'approve'; requestId = $requestId } | ConvertTo-Json)
```

The protocol session is the local caller boundary; hosted calls also require Foundry's platform-injected user identity. For an `azd` local invocation, save the task request as `support-request.json` and run from this folder:

```powershell
Set-Content -NoNewline .\support-request.json '{"task":"My headphones still disconnect after troubleshooting. Escalate this complex issue.","customer":{"name":"Sam","tier":"premium","recentPurchases":["Wireless headphones"],"supportHistory":[]}}'
azd ai agent invoke chapter13-human-in-the-loop --local --protocol invocations --session-id support-demo -f .\support-request.json
```

Copy the returned `approval.request_id` into `approval.json`, then invoke with the same session:

```powershell
Set-Content -NoNewline .\approval.json '{"decision":"approve","requestId":"<request_id from the pending response>"}'
azd ai agent invoke chapter13-human-in-the-loop --local --protocol invocations --session-id support-demo -f .\approval.json
```

For multiple pending requests, submit the `approvals` array described above. Stop the server with Ctrl+C. The separate console project uses shared user-secrets for its model client; this hosted process uses `DefaultAzureCredential`.

## Deploy to Foundry

All commands run from this project folder. See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) and [project/model manifest guidance](../../../README.md#point-the-manifest-at-your-project-and-model) for setup details.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\04-Collaboration\Chapter13.HumanInTheLoop.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<model-deployment>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** Replace the sample project key `mvp-playground-foundry-proj` and endpoint with your Foundry project, and update both the `uses` reference and project-service key. Replace `gpt-5-mini` with `${AZURE_AI_MODEL_DEPLOYMENT_NAME}` so the deployment is supplied by your azd environment.

3. **Deploy:**

   ```powershell
   azd deploy chapter13-human-in-the-loop --no-prompt
   ```

4. **Grant runtime model access.** Run `azd ai agent show chapter13-human-in-the-loop --output json` and read `instance_identity.principal_id`. Check effective role assignments, including inherited grants; if inference access is missing and authorized, assign **Cognitive Services OpenAI User** to that runtime identity at the actual model-hosting Foundry account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck the identity after redeployment.

5. **Invoke:** save the task body as `support-request.json`, then invoke the remote agent:

   ```powershell
   Set-Content -NoNewline .\support-request.json '{"task":"My headphones still disconnect after troubleshooting. Escalate this complex issue.","customer":{"name":"Sam","tier":"premium","recentPurchases":["Wireless headphones"],"supportHistory":[]}}'
   azd ai agent invoke chapter13-human-in-the-loop --protocol invocations --session-id support-demo -f .\support-request.json
   ```

   Copy `approval.request_id` from the response to `approval.json`, then invoke again with the same session. Change `approve` to `reject` to deny escalation; when the response has an `approvals` array, provide one matching decision per pending ID.

   ```powershell
   Set-Content -NoNewline .\approval.json '{"decision":"approve","requestId":"<request_id from the pending response>"}'
   azd ai agent invoke chapter13-human-in-the-loop --protocol invocations --session-id support-demo -f .\approval.json
   ```

Bundled .NET 10 deployment publishes the host together with its referenced Chapter 13 and Shared projects; no Docker image or container registry is required. Deployment permissions and runtime inference access are separate. Hosting and inference incur Azure charges; remove only agents and versions you own using Foundry's agent-scoped controls, and do not run `azd down` against a shared project.

## Telemetry and sensitive data

The `Chapter13.HumanInTheLoop` activity source emits the support-agent spans. **Sensitive-data capture is enabled:** customer prompts, profile context, tool arguments, approval details, and model outputs may appear in traces. Use synthetic data, restrict trace access and retention, and disable sensitive capture before handling confidential customer data. For local traces, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter13-human-in-the-loop`.

## Costs and cleanup

Each support task and resumed approval can make model calls; hosted compute and inference incur Azure charges. Remove only the `chapter13-human-in-the-loop` agent and versions you own through Foundry's agent-scoped management controls. Do not remove shared project resources or run `azd down` against a shared project.

## Pattern tradeoffs

Human approval creates a clear control point before a consequential escalation, but adds latency and requires a reliable way to resume work. Keep approval request IDs bound to the session and caller, make decisions auditable, and ensure denied actions are not presented as completed. The included file store and in-process lock are suitable only for a single instance; multi-replica deployments require shared durable state and distributed coordination.
