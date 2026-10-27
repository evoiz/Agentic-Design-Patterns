# Chapter 12 — Hosted Exception Handling and Recovery

Expose the [Chapter 12 MAF recovery workflow](../Chapter12.ExceptionHandling/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF controls bounded precise-lookup retries, request-local checkpoints, fallback selection, and the final response. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter12-exception-handling` service.

## What it does

Send a sample location address as plain text. The sample index recognizes `1600 Amphitheatre Parkway, Mountain View, CA` and `1 Market Street, San Francisco, CA`. Prefix an address with `transient:` to simulate one transient precise-lookup failure and retry, or with `primary-unavailable:` to simulate an unavailable precise provider and then use the general-area fallback. These prefixes are sample simulation controls, not production flags or a live geocoding service.

Each request starts with fresh workflow state and no persisted conversation history. The precise lookup gets at most two attempts. A successful lookup is described by the primary handler; after retry exhaustion the workflow uses a city-level record when available, then generates one final response. Unknown precise locations also follow the bounded failure/fallback path; an unsupported city has no sample general-area record. The host buffers the completed result, and provider errors and cancellation propagate as request failures.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `1600 Amphitheatre Parkway, Mountain View, CA` | Precise lookup succeeds on the first attempt. | A concise response describes the supplied precise-location record and indicates that precise lookup succeeded. |
| `transient: 1600 Amphitheatre Parkway, Mountain View, CA` | The first precise attempt simulates a transient failure; the second succeeds. | A response based on the precise record after retry; no general-area fallback is needed. |
| `primary-unavailable: 1 Market Street, San Francisco, CA` | Both precise attempts simulate provider unavailability, then the San Francisco area record is used. | The final answer states that precise lookup was unavailable and describes the supplied general area. |
| `primary-unavailable: 9 Example Road, Example City, CA` | The precise lookup is unavailable and no matching general-area sample exists. | The fallback reports that no sample area record is available rather than inventing location facts. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and local Entra ID access or shared Foundry user-secrets; see [Configure Foundry user-secrets](../../../README.md#configure-foundry-user-secrets).

From the repository root:

```powershell
Set-Location .\dotnet\src\05-TrustAndOperations\Chapter12.ExceptionHandling.Hosted
azd ai agent run chapter12-exception-handling
# or, without azd:
dotnet run
```

The local server listens on port 8088 by default; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter12-exception-handling --local --protocol responses '1600 Amphitheatre Parkway, Mountain View, CA'
azd ai agent invoke chapter12-exception-handling --local --protocol responses 'transient: 1600 Amphitheatre Parkway, Mountain View, CA'
azd ai agent invoke chapter12-exception-handling --local --protocol responses 'primary-unavailable: 1 Market Street, San Francisco, CA'
```

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. A blank or absent API key selects the signed-in developer identity locally. Stop the server with Ctrl+C.

## Deploy to Foundry

All commands run from this project folder. See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) and [project/model manifest guidance](../../../README.md#point-the-manifest-at-your-project-and-model) for setup details.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\05-TrustAndOperations\Chapter12.ExceptionHandling.Hosted
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
   azd deploy chapter12-exception-handling --no-prompt
   ```

4. **Grant runtime model access.** Run `azd ai agent show chapter12-exception-handling --output json` and read `instance_identity.principal_id`. Check effective role assignments, including inherited grants; if inference access is missing and authorized, assign **Cognitive Services OpenAI User** to that runtime identity at the actual model-hosting Foundry account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck the identity after redeployment.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter12-exception-handling --protocol responses 'transient: 1600 Amphitheatre Parkway, Mountain View, CA'
   ```

Bundled .NET 10 deployment publishes the host together with its referenced Chapter 12 and Shared projects; no Docker image or container registry is required.

## Telemetry and sensitive data

Agent spans use the `Chapter12.ExceptionHandling` activity source and cover primary lookup, recovery decisions, fallback, and final response. **Sensitive-data capture is enabled:** prompts, sample addresses, intermediate results, and responses may appear in exported traces. Use non-sensitive inputs, restrict trace access and retention, and disable sensitive capture before handling confidential addresses. For local traces, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter12-exception-handling`.

## Costs and cleanup

A failed precise lookup can make two lookup attempts before fallback; the primary, fallback, and final-response handlers may also call the model. Hosting and inference incur Azure charges. Remove only the `chapter12-exception-handling` agent and versions you own through Foundry's agent-scoped management controls. Do not run `azd down` against a shared project.

## Pattern tradeoffs

Retries are bounded and appropriate here because the simulated lookup has no side effects. Real operations need explicit transient-error classification, timeouts, and idempotency controls. A general-area fallback is less precise than an exact lookup and should be labeled; the sample checkpoints are request-local, so durable restart recovery requires a persistent workflow store.
