# Enterprise Extension 01 - Hosted Agent Harness

Expose the reusable [Agent Harness pattern](../Extension01.AgentHarness/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host references the console project's `HarnessRunner` instead of duplicating its prompts or capturing console output. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `extension01-agent-harness` service.

## What it does

Send one non-empty plain-text request of at most 4,000 characters. The request should name `checkout` or `identity` as a whole word. The runner creates a fresh harness, todo provider, evidence list, tools, and call budget, then uses read-only tools to inspect synthetic service health and the matching runbook. It returns JSON with `status` (`completed` or `incomplete`), the actual final model `answer`, request-wide `modelCalls`, todo snapshots, evidence receipts, and `incompleteReason`. The caller's latest user message supplies the task; prior assistant messages and caller-supplied instructions do not become system authority.

Completion requires a recorded work plan with no unfinished todos, at least one supported named target, and both status and runbook evidence for every supported service named in the request. The deterministic status reports missing plan, unfinished todos, missing supported target, or missing required evidence. The answer remains recommendations only; the tools read fixed synthetic data and never query production or execute changes. Budget exhaustion, cancellation, empty model output, and provider failures use the host error path rather than becoming successful synthetic answers.

Each request is isolated with a fresh session and request-scoped tools, evidence, todo state, call budget, and three-minute deadline; the isolation key uses the Responses request ID even when the caller provides no user header. No conversation state is persisted or resumed. The outer Responses status describes protocol processing; inspect the JSON result's inner `status` before treating the investigation as complete. Model calls are non-streaming; a streaming Responses request buffers the completed result and emits one final update, not intermediate tool arguments or progress. The harness allows at most 12 model-client calls and two outer todo-loop passes. `store: false` is recommended.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Investigate checkout latency. Inspect service health and the runbook, then recommend next steps.` | Reads synthetic checkout status and `CHECKOUT-01`, and records todos and evidence. | A JSON result with checkout status/runbook receipts and a recommendation; it is `completed` only if all todo and evidence checks pass. The synthetic runbook recommends database on-call review above 90% pool saturation; no escalation is performed. |
| `Investigate identity authentication latency. Check health and the approved runbook, then summarize next steps.` | Reads synthetic identity status and `IDENTITY-01`. | Result includes evidence for both checks; the runbook requests timestamps and correlation IDs if the service is healthy, not passwords or tokens. Completion still depends on the deterministic work/evidence checks. |
| `Compare the synthetic checkout and identity service health and runbooks.` | Names two supported targets, so completion requires both status and runbook evidence for each. | Evidence must contain four service/kind combinations; a single pair is insufficient and yields `incomplete`. |
| `Investigate the payments service and recommend a fix.` | Names no supported target (`checkout` or `identity`). | The result is `incomplete`; if the work plan and todo checks complete, its reason is `The request must name a supported target service: checkout or identity.` Otherwise the earlier plan/todo check reports the missing work. The agent is instructed not to invent evidence for payments. |

## Run locally

Prerequisites: .NET 10 SDK, PowerShell 7, Azure CLI, azd with the `azure.ai.agents` extension, a Foundry tool-capable chat deployment, and developer identity access to model inference. This host always uses Entra ID: `DefaultAzureCredential` locally and managed identity in Azure. It never reads or uses an API key, even if one exists in shared user-secrets. Configure the endpoint/model through shared user-secrets or environment variables as below; see [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets).

From the repository root:

```powershell
Set-Location .\dotnet\src\05-TrustAndOperations\Extension01.AgentHarness.Hosted
$env:FOUNDRY_PROJECT_ENDPOINT = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = '<chat-model-deployment>'
$env:AZURE_TENANT_ID = '<tenant-id>'
azd ai agent run extension01-agent-harness
# Or, without azd:
dotnet run
```

`Foundry:ProjectEndpoint` and `Foundry:ModelDeployment` are checked first, so shared user-secrets win over `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME`. To override shared user-secrets locally, set `$env:Foundry__ProjectEndpoint` and `$env:Foundry__ModelDeployment` instead. The server listens on port 8088 by default; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke extension01-agent-harness --local --protocol responses 'Investigate checkout latency. Check health and the runbook and recommend next steps.'
azd ai agent invoke extension01-agent-harness --local --protocol responses 'Investigate identity authentication latency.'
azd ai agent invoke extension01-agent-harness --local --protocol responses 'Investigate the payments service and recommend a fix.'
```

For a direct local request, the protocol accepts a model, input, and `store` option:

```powershell
$body = @{
    model = 'extension01-agent-harness'
    input = 'Investigate checkout latency. Check its health and runbook, then recommend next steps.'
    stream = $false
    store = $false
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:8088/responses' -ContentType 'application/json' -Body $body
```

Stop the server with Ctrl+C. From the repository root, the deterministic hosted HTTP and harness regression tests can be run with `dotnet test .\dotnet\tests\05-TrustAndOperations\AgentHarness.Tests`; the test model is scripted and does not call Foundry.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for prerequisites and background. All commands below run from this project folder.

1. **Sign in and create this folder's azd environment:**

   ```powershell
   Set-Location .\dotnet\src\05-TrustAndOperations\Extension01.AgentHarness.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   azd env set HARNESS_ENABLE_SENSITIVE_TELEMETRY false
   ```

2. **Point [`azure.yaml`](azure.yaml) at your project and model.** Replace the sample `mvp-playground-foundry-proj` project key, its endpoint, and the service's `uses` reference with your Foundry project. The service maps `AZURE_AI_PROJECT_ENDPOINT` to `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` into the hosted process; the azd values above must match your project and chat deployment. To turn off sensitive capture, map the manifest value to `${HARNESS_ENABLE_SENSITIVE_TELEMETRY}`; the azd environment command in step 1 then applies. The sample manifest enables sensitive capture by default.

3. **Deploy only this service:**

   ```powershell
   azd deploy extension01-agent-harness --no-prompt
   ```

4. **Verify runtime model access.** Run `azd ai agent show extension01-agent-harness --output json` and read the current `instance_identity.principal_id`. Check effective role assignments, including inherited grants, on the actual model-hosting Foundry account. If inference access is missing and the RBAC change is authorized, assign **Cognitive Services OpenAI User** (`5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) to that runtime principal at the account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck after redeployments, which can change the runtime identity.

5. **Invoke the deployed agent after access is effective:**

   ```powershell
   azd ai agent invoke extension01-agent-harness --protocol responses 'Investigate checkout latency. Check health and the runbook and recommend next steps.'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Extension01.AgentHarness and Shared projects; `.agentignore` excludes secrets and build artifacts, and no container registry is required.

## Telemetry and sensitive data

The `Extension01.AgentHarness` activity source records harness activity. The deployment manifest enables `Harness__EnableSensitiveTelemetry`, so prompts, tool results, and model responses may be captured in traces; local sensitive capture is off by default. **This sample uses synthetic incidents; set sensitive capture to false before processing confidential requests, and restrict telemetry access and retention.** To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `extension01-agent-harness`.

## Costs and cleanup

The request is limited to 12 model-client calls and a three-minute deadline, but this is not a token or currency cap; provider retries may occur within a counted call. Hosting, model calls, context compaction, and enabled telemetry may incur charges. Remove only the `extension01-agent-harness` agent and versions you own with Foundry's agent-scoped controls. Never run `azd down` against a shared project.

## Pattern tradeoffs

The harness makes operational boundaries explicit but does not make model judgments deterministic. A recorded plan can be poor, or completed todos can accompany an inaccurate recommendation. Add independent evaluation for semantic correctness, and explicit authorization/idempotency before introducing write tools. Durable state requires a separate storage and resume design.

## References

- [Agent Harness guide and pattern combinations](../Extension01.AgentHarness/README.md)
- [Official Foundry MAF local-tools sample](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/local-tools)
- [Foundry hosted agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
