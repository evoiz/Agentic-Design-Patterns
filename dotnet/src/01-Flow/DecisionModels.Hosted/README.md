# Decision Models - Hosted Support Decisions

Expose the reusable [Decision Models workflow](../DecisionModels/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. Hosting preserves the typed decisions and deterministic review gates; it does not introduce a different decision algorithm. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `decision-models` service.

## What it does

Send one non-empty, text-only support request as the latest user message. The `Decisions:Provider` setting selects `microsoft` or `openai`; caller text cannot change the provider. Each request performs three typed judgments: support team selection, severity scoring, and refund-request assessment. Application policy then routes to a specialist draft only when the selected team is not `unclear` and its probability is at least 0.8, and the refund probability is below 0.5. Otherwise, it returns a fixed human-review or refund-review requirement. Severity is marked `urgent` at a score of 1.5 or above and `normal` below it.

The final assistant text is JSON containing `Decision` (the input, full distributions, usage, disposition, and priority) and `Response` (the specialist draft or review requirement). Only the latest user message is assessed; earlier history and sessions do not carry business state. There is no persistent ticket queue, approval transaction, or memory. A review result does not notify a reviewer. Requests never issue refunds or modify systems. Responses streaming buffers the complete policy-checked result rather than exposing partial output. Errors and cancellation propagate; refusals are not converted into approval.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Checkout is down for every customer.` | Runs team, severity, and refund decisions; may route to the technical specialist if its team probability passes the gate. | JSON decision report; an admitted non-refund request gets a concise draft, and severity at or above 1.5 is marked urgent. Routing remains a model judgment. |
| `Please return the charge for the duplicate invoice.` | Assesses billing team, severity, and whether a refund is requested. | If the billing choice meets the confidence threshold and refund probability is at least 0.5, `Response` is exactly `Requires authorized refund review. No refund, queue write, or account action was performed.` No refund is issued. |
| `I cannot sign in to my account.` | Assesses the account team and reported impact, then applies the same deterministic gates. | A sufficiently confident account classification with no refund signal may produce an account-specialist draft; uncertain classification returns a human-review requirement instead. |
| `I need help with a problem.` | Exercises ambiguous support triage. | If the decision model chooses `unclear` or the selected team probability is below 0.8, the response is exactly `Requires human review because the team is unclear or its probability is below 0.8. No queue write was performed.` The classifier's choice is not guaranteed. |

## Run locally

Prerequisites: .NET 10 SDK, Azure CLI, azd with the `azure.ai.agents` extension, a Foundry project, model-inference access for your developer identity, a generative chat deployment, and one decision-model deployment for the selected provider. The host always uses Entra ID locally and managed identity in Azure; it ignores API keys. Configure shared user-secrets as described in [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets), or use the environment variables below.

From the repository root:

```powershell
Set-Location .\dotnet\src\01-Flow\DecisionModels.Hosted
$env:FOUNDRY_PROJECT_ENDPOINT = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = '<generative-chat-deployment>'
$env:Foundry__ModelDeployments__decision = '<microsoft-decision-deployment>'
$env:Foundry__ModelDeployments__openaiDecision = '<gpt-6-luna-deployment>'
$env:Decisions__Provider = 'microsoft'
$env:AZURE_TENANT_ID = '<tenant-id>'
azd ai agent run decision-models
# Or, without azd:
dotnet run -- --urls http://localhost:8088
```

The endpoint and default chat deployment can also be provided in shared `Foundry:ProjectEndpoint` and `Foundry:ModelDeployment` user-secrets; the aliases are `Foundry:ModelDeployments:decision` and `Foundry:ModelDeployments:openaiDecision`. Only the alias for the selected provider is required. The Microsoft provider uses Microsoft-Decision-1; the OpenAI provider uses a gpt-6-luna deployment. Set `Decisions__Provider` to `openai` to use that backend. `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` are supported endpoint/chat deployment fallbacks.

In a second terminal, from the same folder:

```powershell
azd ai agent invoke decision-models --local --protocol responses 'Checkout is down for every customer.'
azd ai agent invoke decision-models --local --protocol responses 'Please return the charge for the duplicate invoice.'
azd ai agent invoke decision-models --local --protocol responses 'I need help with a problem.'
```

For an explicit local Responses request, including the model and no-history options:

```powershell
$body = @{
    model = 'decision-models'
    input = 'Checkout is down for every customer.'
    stream = $false
    store = $false
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:8088/responses' -ContentType 'application/json' -Body $body
```

Stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for prerequisites and background. All commands below run from this project folder.

1. **Sign in and create this folder's azd environment:**

   ```powershell
   Set-Location .\dotnet\src\01-Flow\DecisionModels.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<generative-chat-deployment>'
   azd env set FOUNDRY_MODEL_DEPLOYMENT_DECISION '<microsoft-decision-deployment>'
   azd env set FOUNDRY_MODEL_DEPLOYMENT_OPENAI_DECISION '<gpt-6-luna-deployment>'
   azd env set DECISIONS_PROVIDER 'microsoft'
   ```

2. **Point [`azure.yaml`](azure.yaml) at your project and models.** Replace the sample `mvp-playground-foundry-proj` project key, its endpoint, and the service's `uses` reference with your Foundry project. Replace the sample chat deployment with `${AZURE_AI_MODEL_DEPLOYMENT_NAME}` and the decision alias values with `${FOUNDRY_MODEL_DEPLOYMENT_DECISION}` and `${FOUNDRY_MODEL_DEPLOYMENT_OPENAI_DECISION}`. Set `Decisions__Provider` to `${DECISIONS_PROVIDER}`. Keep aliases matched to your provider deployments; only the selected provider's alias is required at runtime.

3. **Deploy only this service:**

   ```powershell
   azd deploy decision-models --no-prompt
   ```

4. **Verify runtime inference access.** Run `azd ai agent show decision-models --output json` and read the current `instance_identity.principal_id`. Check effective assignments, including inherited grants, on the actual account hosting each selected model. If inference access is missing and the RBAC change is authorized, assign **Cognitive Services OpenAI User** (`5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) to the runtime principal at each required model-hosting account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck after redeployment, which can change the runtime identity. Do not substitute the deploying user, project identity, or blueprint, broaden to subscription scope, or use an API key as a workaround.

5. **Invoke the deployed agent after access is effective:**

   ```powershell
   azd ai agent invoke decision-models 'Checkout is down for every customer.'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced DecisionModels and Shared projects; no container registry is required.

## Model aliases and policy

The service uses one generative chat deployment plus a separate decision deployment. `microsoft` maps to the `decision` alias for Microsoft-Decision-1; `openai` maps to `openaiDecision` for gpt-6-luna. Keep `AZURE_AI_MODEL_DEPLOYMENT_NAME` for generative specialists and do not substitute a generic chat model for either decision deployment. The sample manifest's provider and aliases are defaults to replace with your own configuration.

The thresholds are illustrative application policy, not calibrated model guarantees. Ambiguity/low team probability takes precedence over refund review; otherwise refund probability at least 0.5 requests refund review. A request requiring review returns a response to the caller only—it is not a persisted queue or a human approval transaction.

## Telemetry and sensitive data

The `DecisionModels` activity source records support-decision metadata. Specialist-agent telemetry has sensitive content enabled, so requests and generated drafts may appear in traces. **Use synthetic support requests and restrict telemetry access and retention; disable sensitive capture before processing private customer data.** To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `decision-models`.

## Costs and cleanup

Each request makes three decision calls and, only when admitted by policy, one generative specialist call. Hosted compute, model inference, and enabled telemetry may incur charges. Remove only the `decision-models` agent and versions you own with Foundry's agent-scoped controls. Never run `azd down` against a shared project or remove shared model deployments.

## Pattern tradeoffs

Misrouting can remain hidden even when the response schema is valid. Probabilities can be overconfident under distribution shift, wording and option order can affect results, and a single score can hide a multimodal distribution. Repeated decisions add network round trips, while review thresholds trade automation coverage against error risk. A generative specialist can still draft an incorrect response after an accurate route; keep authorization and actions outside model judgment.
