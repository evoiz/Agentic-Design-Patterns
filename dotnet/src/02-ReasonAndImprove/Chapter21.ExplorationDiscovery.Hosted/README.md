# Chapter 21 - Hosted Exploration & Discovery

Expose the [Chapter 21 MAF proposal-and-review workflow](../Chapter21.ExplorationDiscovery/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter21-exploration-discovery` service. The hosted service runs the bounded MAF workflow; Foundry Deep Research remains a separate console variant.

## What it does

The latest user message supplies one research topic of at most 4,000 characters. The MAF workflow creates a structured proposal with a focused question, three to five investigation steps, evidence needs, and risks. Three reviewers assess experimental usefulness, impact, and novelty. The workflow accepts a proposal only when all reviewers decide `Accept`, each overall score is at least 7/10, and no reviewer flags ethical concerns. Otherwise, it revises once using reviewer feedback and returns the final proposal and assessments even if acceptance criteria remain unmet.

A request is bounded to at most two review rounds and three minutes (up to eight model calls: one proposal and three parallel reviewer calls per round). The host buffers the finished result into one response. Every call has a fresh session; no conversation history or proposal state is persisted across requests. The explorer is instructed to use the request and general model knowledge only and not to claim it searched literature, browsed the web, ran code, collected data, or performed experiments. The returned proposal is not a literature review or experimental result.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Explore whether adaptive batching can reduce inference cost without harming tail latency.` | Produces a testable research question and plan, then runs the three reviewer perspectives. | Completed proposal, evidence needs, risks, and reviewer assessments; status may be `accepted` or `needs human review`. |
| `Investigate how to compare two database-pool settings for checkout latency.` | Requests a practical investigation with measurable steps. | Three to five ordered proposal steps plus evidence and risk lists; no experiment is executed. |
| `Search recent papers and tell me whether this method has already been proven.` | The explorer is explicitly limited to caller input and general model knowledge and must not claim a literature search occurred. | Returns an exploration proposal rather than sourced findings; the final text states it is unverified, not a literature review or experimental result. |
| `Tell me the definitive cause of last week's clinical-trial outcome.` | Treats the request as a topic for investigation rather than evidence of a known result. | A proposal/reviewer report may identify assumptions and evidence needed; it does not verify the outcome or offer a guaranteed acceptance. |

## Run locally

Prerequisites: .NET 10 SDK, Azure CLI, azd with the `azure.ai.agents` extension, a Foundry chat deployment, and either shared Foundry user-secrets or developer identity access. Local inference uses `DefaultAzureCredential` when no API key is configured, or the shared user-secrets client when one is present; Azure execution uses managed identity. See [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets).

From the repository root:

```powershell
Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter21.ExplorationDiscovery.Hosted
azd ai agent run chapter21-exploration-discovery
# Or, without azd:
dotnet run
```

The local server listens on port 8088 by default; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter21-exploration-discovery --local --protocol responses 'Explore whether adaptive batching can reduce inference cost without harming tail latency.'
azd ai agent invoke chapter21-exploration-discovery --local --protocol responses 'Investigate how to compare two database-pool settings for checkout latency.'
azd ai agent invoke chapter21-exploration-discovery --local --protocol responses 'Search recent papers and propose what evidence would be needed to assess adaptive batching.'
```

Stop the server with Ctrl+C. An optional configured model alias can be selected by passing `--deployment <alias>` when starting the host.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for prerequisites and background. All commands below run from this project folder.

1. **Sign in and create this folder's azd environment:**

   ```powershell
   Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter21.ExplorationDiscovery.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   ```

2. **Point [`azure.yaml`](azure.yaml) at your project and model.** Replace the sample `mvp-playground-foundry-proj` project key, its endpoint, and the service's `uses` reference with your Foundry project. Replace the sample model value with `${AZURE_AI_MODEL_DEPLOYMENT_NAME}` so the environment value above selects your chat deployment.

3. **Deploy only this service:**

   ```powershell
   azd deploy chapter21-exploration-discovery --no-prompt
   ```

4. **Verify runtime model access.** Run `azd ai agent show chapter21-exploration-discovery --output json` and read the current `instance_identity.principal_id`. Check effective assignments, including inherited grants, on the actual model-hosting Foundry account. If inference access is missing and the RBAC change is authorized, assign **Cognitive Services OpenAI User** (`5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) to that runtime principal at the account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck after redeployments, which can change the runtime identity.

5. **Invoke the deployed agent after access is effective:**

   ```powershell
   azd ai agent invoke chapter21-exploration-discovery --protocol responses 'Explore whether adaptive batching can reduce inference cost without harming tail latency.'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 21 and Shared projects; no Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter21.ExplorationDiscovery` activity source traces the explorer and reviewer calls. Sensitive-data capture is enabled, so research topics, proposals, reviewer prompts, and assessments may appear in traces. **Use non-sensitive topics and restrict telemetry access and retention; disable sensitive capture before handling confidential material.** To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter21-exploration-discovery`.

## Costs and cleanup

Each request uses up to eight model calls within the three-minute time budget; hosted compute and enabled telemetry may also incur charges. Remove only the `chapter21-exploration-discovery` agent and versions you own using Foundry's agent-scoped controls. Never run `azd down` against a shared project.

## Pattern tradeoffs

The generated proposal and its reviewers can share model biases, and agreement is not proof of novelty, feasibility, or truth. Use independent evidence and qualified human review before acting on a proposal. The two-round and three-minute limits bound work but can leave reviewer concerns unresolved.
