# Extension 01 - Hosted Agent Harness

[HarnessHostedAgent](HarnessHostedAgent.cs) delegates to the console project's
[HarnessRunner](../Extension01.AgentHarness/HarnessRunner.cs), using the shared stateless
Responses adapter. Prompts, todo checks and budgets are not duplicated by the host.
See the [pattern guide](../Extension01.AgentHarness/README.md) first.

## Request and result

Send a self-contained question about synthetic `checkout` or `identity` services.
Input must be nonempty and at most 4,000 characters:

```json
{
  "model": "extension01-agent-harness",
  "input": "Investigate checkout latency. Inspect health and its runbook.",
  "stream": false,
  "store": false
}
```

Assistant `output_text` is JSON with `status`, `answer`, `modelCalls`, `todos`,
`evidence` and `incompleteReason`. Inspect the **inner status**, not only the outer
Responses completion status. Completion requires finished todos and both tool receipts
for every supported service named in the input. No production operations are performed.
Budget exhaustion, cancellation, provider failures and empty output propagate as errors.

## State and execution

Every request creates fresh tools, evidence, todo state, session and model-call budget.
Only the latest user message is used. [RequestIsolationKeyProvider](RequestIsolationKeyProvider.cs)
uses the response ID even without a user header. Sessions serialize no application state.
There is no durable conversation/resume, approval wait or background recovery.

Responses streaming buffers one final JSON update, not intermediate tools or reasoning.
Bounds remain three minutes of cooperative cancellation, 12 model-client calls and two
outer todo-loop passes. Provider retries inside a call are not separately counted.

## Local configuration and invocation

Use [.NET 10 and shared hosting prerequisites](../../../HOSTING.md). The host **never uses
an API key**: local inference uses `DefaultAzureCredential`; Foundry runtime inference
uses system-assigned managed identity. Your developer identity needs inference access.

```powershell
$env:Foundry__ProjectEndpoint = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:Foundry__ModelDeployment = '<tool-capable-chat-deployment>'
dotnet run --project dotnet\src\05-TrustAndOperations\Extension01.AgentHarness.Hosted
```

Runtime fallbacks are `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME`;
canonical settings win. From another terminal:

```powershell
$body = @{ model = 'extension01-agent-harness'; input = 'Investigate checkout latency.'; stream = $false; store = $false } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:8088/responses' -ContentType 'application/json' -Body $body
dotnet test dotnet\tests\05-TrustAndOperations\AgentHarness.Tests
```

## Deploy and operate

Follow [shared project selection, RBAC, packaging and cleanup](../../../HOSTING.md).
Replace the sample project reference and use reader-owned settings; do not reinitialize
the multi-service manifest. From `dotnet`:

```powershell
azd deploy extension01-agent-harness --no-prompt
azd ai agent show extension01-agent-harness --output json
azd ai agent invoke extension01-agent-harness --protocol responses 'Investigate checkout latency.'
```

Before invocation, verify **Cognitive Services OpenAI User** or equivalent for the
current runtime `instance_identity.principal_id` at the actual model-hosting account.
Allow propagation and recheck after redeployment. Bundled publishing includes
`Extension01.AgentHarness`, `Shared`, `Shared.Hosting` and MAF harness dependencies.

`Harness__EnableSensitiveTelemetry` enables synthetic-demo trace capture in the manifest;
disable it before private input. Hosting, inference, retries and compaction incur costs.
Plans and receipts do not prove semantic correctness; add independent evaluation when needed.
