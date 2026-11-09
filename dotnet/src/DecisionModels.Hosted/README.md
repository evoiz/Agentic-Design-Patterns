# Hosted Decision Models

The [decision-model workflow](../DecisionModels/README.md) runs unchanged
behind the official MAF Foundry Responses integration. Hosting is not a new pattern.
The project is `DecisionModels.Hosted`; its deployed service/agent identifier
remains **`decision-models`**.

## Code and request contract

[DecisionSupportAgent](DecisionSupportAgent.cs) rejects nontext input and delegates
the latest user request to `DecisionAgentWorkflow`. Common session/streaming behavior
comes from [StatelessWorkflowAgent](../Shared.Hosting/StatelessWorkflowAgent.cs).

Each request performs three typed decisions, applies deterministic policy and optionally
generates a specialist draft. Assistant text is JSON with `Decision` (input, distributions,
usage, disposition and priority) and `Response` (draft or review requirement). Only the
latest user message is assessed; requests are independent. Streaming buffers the final
policy-checked result. Cancellation, malformed output, refusals and provider failures
propagate as errors.

No persistent tickets, human notifications, refunds or account actions occur. Review
outcomes describe required action; they are not completed approvals.

## Configuration and local run

Use .NET 10 and the [model deployment aliases](../DecisionModels/README.md#setup-and-run).
The host always uses Entra ID (`DefaultAzureCredential` locally, managed identity in
Azure), ignoring shared API keys. Your identity needs inference access. Endpoint/model
configuration uses shared user-secrets, `Foundry__*` overrides and runtime fallbacks
documented in [shared hosting](../../HOSTING.md).

```powershell
$env:Foundry__ProjectEndpoint = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:Foundry__ModelDeployment = '<generative-chat-deployment>'
$env:Foundry__ModelDeployments__decision = '<microsoft-decision-deployment>'
$env:Decisions__Provider = 'microsoft'
dotnet run --project dotnet\src\DecisionModels.Hosted -- --urls http://localhost:8088
```

For `Decisions__Provider = 'openai'`, configure `Foundry__ModelDeployments__openaiDecision`
instead. The provider is host configuration, not caller text. From a second terminal:

```powershell
$body = @{ model = 'decision-models'; input = 'Checkout is down for every customer.'; stream = $false; store = $false } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:8088/responses' -ContentType 'application/json' -Body $body
```

## Deployment and packaging

Follow [shared hosting setup, runtime RBAC and cleanup](../../HOSTING.md). Replace
sample project/model settings in this directory's `azure.yaml` with reader-owned values, keeping
the service key `decision-models`. Deploy only that service:

```powershell
Set-Location .\dotnet\src\DecisionModels.Hosted
azd deploy decision-models --no-prompt
azd ai agent show decision-models --output json
azd ai agent invoke decision-models 'Checkout is down for every customer.'
```

Before invocation, verify the current `instance_identity.principal_id` has **Cognitive
Services OpenAI User** or equivalent on every model-hosting account; allow RBAC
propagation. Bundled publishing must include `DecisionModels.dll`,
`AgenticDesignPatterns.Shared.Hosting.dll`, dependencies and runtime files, never secrets.

Three decision calls plus at most one chat call incur costs. Sensitive prompt/response
capture is disabled in specialist agent spans.
Evaluate calibration, abstention and downstream outcomes separately; hosting does not
establish model accuracy.
