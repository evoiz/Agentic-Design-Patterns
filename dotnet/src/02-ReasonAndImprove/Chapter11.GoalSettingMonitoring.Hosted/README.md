# Chapter 11 — Hosted Goal Setting and Monitoring

Expose the [Chapter 11 MAF goal-setting workflow](../Chapter11.GoalSettingMonitoring/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The workflow generates Python code, reviews it against explicit goals, and stops when a model judge confirms success or the iteration budget is exhausted. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter11-goal-setting-monitoring` service.

## What it does

Send a use case as plain text to apply the sample goals and five-iteration budget, or send a JSON object with `useCase`, `goals`, and optional `maxIterations`:

```json
{
  "useCase": "Write Python code to parse a CSV file and report row counts.",
  "goals": ["Use the standard library", "Handle an empty file", "Print the row count"],
  "maxIterations": 3
}
```

`useCase` must be non-empty and at most 2,000 characters; `goals` must contain 1–10 non-empty strings of at most 300 characters each; `maxIterations` must be 1–5. Plain text uses the sample goals: simple code, functional correctness, edge-case handling, positive-integer input, and a few printed examples. The completed response reports whether the goals were met, iterations used, and final Python code. Generated code is not saved or executed by the host.

Each request runs in a fresh session partition keyed by the Responses `response_id`, even if a service-to-service caller omits the platform user header. Caller history is not reused. The Responses streaming endpoint buffers the completed workflow result; cancellation propagates through model calls. The workflow makes generation, review, and judge calls per iteration, up to the selected budget.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Write Python code to find the longest binary gap of a positive integer.` | Plain-text request uses the sample goals and five-iteration budget. | A response reports goal status and iteration count, followed by final Python code for the requested use case. |
| `Write Python code to parse a CSV file and report row counts.` | Plain text uses the sample goals, including positive-integer input, even though the use case differs. | The loop returns the final generated code and whether the judge reports all sample goals met; it may stop before five iterations. |
| `{"useCase":"Write a CSV row counter.","goals":["Use the standard library","Handle an empty file","Print the row count"],"maxIterations":3}` | Structured input supplies custom success goals and a three-iteration limit. | The response contains a goal status, iterations used (no more than three), and final Python code. |
| `{"useCase":"Write a CSV row counter.","goals":["Count rows"],"maxIterations":6}` | Structured input exceeds the iteration maximum. | The request is rejected by validation; the allowed `maxIterations` range is 1–5. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and local Entra ID access or shared Foundry user-secrets; see [Configure Foundry user-secrets](../../../README.md#configure-foundry-user-secrets).

From the repository root:

```powershell
Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter11.GoalSettingMonitoring.Hosted
azd ai agent run chapter11-goal-setting-monitoring
# or, without azd:
dotnet run
```

The local server listens on `http://localhost:8088`; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter11-goal-setting-monitoring --local --protocol responses 'Write Python code to find the longest binary gap of a positive integer.'
azd ai agent invoke chapter11-goal-setting-monitoring --local --protocol responses '{"useCase":"Write a CSV row counter.","goals":["Use the standard library","Handle an empty file","Print the row count"],"maxIterations":3}'
azd ai agent invoke chapter11-goal-setting-monitoring --local --protocol responses '{"useCase":"Write a CSV row counter.","goals":["Count rows"],"maxIterations":6}'
```

Local model access uses shared `Foundry` user-secrets or `Foundry__*` environment variables; a blank API key uses the signed-in developer identity. Stop the server with Ctrl+C.

## Deploy to Foundry

All commands run from this project folder. See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) and [project/model manifest guidance](../../../README.md#point-the-manifest-at-your-project-and-model) for setup details.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter11.GoalSettingMonitoring.Hosted
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
   azd deploy chapter11-goal-setting-monitoring --no-prompt
   ```

4. **Grant runtime model access.** Run `azd ai agent show chapter11-goal-setting-monitoring --output json` and read `instance_identity.principal_id`. Check effective role assignments, including inherited grants; if inference access is missing and authorized, assign **Cognitive Services OpenAI User** to that runtime identity at the actual model-hosting Foundry account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck the identity after redeployment.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter11-goal-setting-monitoring --protocol responses 'Write Python code to find the longest binary gap of a positive integer.'
   ```

Bundled .NET 10 deployment publishes the host together with its referenced Chapter 11 and Shared projects; no Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter11.GoalSettingMonitoring` activity source records the goal-setting run and goal/iteration tags; agent spans cover generation, review, and judge calls. **Sensitive-data capture is enabled:** caller use cases, goals, generated code, reviewer feedback, and model outputs may appear in traces. Use non-sensitive prompts, restrict trace access and retention, and disable sensitive capture before processing confidential data. For local traces, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter11-goal-setting-monitoring`.

## Costs and cleanup

Each iteration makes three model calls, up to the requested maximum of five, in addition to hosted compute. Remove only the `chapter11-goal-setting-monitoring` agent and versions you own through Foundry's agent-scoped management controls. Do not run `azd down` against a shared project.

## Pattern tradeoffs

Separate generation, review, and judgment make progress visible, but the reviewer and judge can still make correlated or incorrect assessments. A bounded loop limits latency and cost; it does not prove correctness. Use deterministic tests and human review before executing or adopting generated code.
