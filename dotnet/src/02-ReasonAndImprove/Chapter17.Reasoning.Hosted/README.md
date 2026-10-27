# Chapter 17 - Hosted Reasoning Agent

Expose the [Chapter 17 MAF reasoning demos](../Chapter17.Reasoning/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host processes caller-provided prompts using one of four bounded reasoning scenarios; this folder is self-contained with its own [`azure.yaml`](azure.yaml) defining the `chapter17-reasoning` service.

## What it does

- **Input:** Latest user message as Responses `input` text. Prefixes select a scenario: `cot:` or `reason:`, `self-correction:` or `selfcorrection:`, `react:`, and `deep-search:` or `deepsearch:`. Unprefixed text, or text with an unrecognized prefix, uses chain-of-thought mode with the full text as input.
- **Output:** One completed text response. Chain-of-thought mode returns a direct answer and concise user-facing rationale, not private chain-of-thought. Self-correction returns outcome, round count, final draft, and review feedback. ReAct returns the answer from the bounded factorial tool. Deep-search returns a synthesis of model-knowledge notes without claiming live search or verified citations.
- **Bounds:** Self-correction uses at most three generation/review rounds; a draft is accepted only if the reviewer approves it and code confirms it has 150 characters or fewer. If the budget runs out, the last draft is returned unchanged with outcome `MaxRoundsReached`. ReAct exposes only factorial for integers 0 through 20, not arbitrary code execution. Deep-search plans at most three queries per round and runs at most two research rounds.
- **State:** Each request starts with fresh workflow state and retains no conversation history. The completed run is buffered into one final Responses update; intermediate stages may be available in configured traces.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `cot: Explain why daylight varies over the year.` | Selects the reasoned-answer scenario. | Direct answer with a concise, user-facing rationale; no hidden step-by-step reasoning. |
| `self-correction: Write a short post about a new eco-friendly product.` | Generates and reviews a draft, revising within the three-round budget. | Outcome, rounds, final draft, and review. `Accepted` requires reviewer approval and at most 150 characters; otherwise `MaxRoundsReached` with the last draft, which may exceed 150. |
| `react: Calculate 10 factorial.` | Selects ReAct and requires a `calculate_factorial` tool call. | Exact tool result and brief explanation; the supported range is 0–20. |
| `deep-search: Summarize the development of electric vehicles.` | Plans focused queries, researches with model knowledge, reflects, and synthesizes. | Bounded synthesis that identifies model-knowledge limits; no live browsing or citations are claimed. |
| `Summarize the main causes of tides.` | No recognized prefix is present, so the request defaults to chain-of-thought mode. | Direct answer with a concise rationale. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, a Foundry project and chat-model deployment, and shared Foundry settings ([configure Foundry user-secrets](../../../README.md#configure-foundry-user-secrets)). Local development uses the configured API key when present; otherwise it authenticates with `DefaultAzureCredential`.

From the repository root:

```powershell
Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter17.Reasoning.Hosted
azd ai agent run chapter17-reasoning
# or, without azd:
# dotnet run --project .\Chapter17.Reasoning.Hosted.csproj
```

In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter17-reasoning --local --protocol responses 'cot: Explain why daylight varies over the year.'
azd ai agent invoke chapter17-reasoning --local --protocol responses 'self-correction: Write a short post about a new eco-friendly product.'
azd ai agent invoke chapter17-reasoning --local --protocol responses 'react: Calculate 10 factorial.'
azd ai agent invoke chapter17-reasoning --local --protocol responses 'deep-search: Summarize the development of electric vehicles.'
```

Stop the host with Ctrl+C when finished; keep the local endpoint private.

## Deploy to Foundry

Follow the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents). Run azd commands in this project folder.

1. **Sign in and create an azd environment:**

   ```powershell
   Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter17.Reasoning.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** Replace the sample `mvp-playground-foundry-proj` project key and endpoint with your own, update the service's `uses` entry, and set the model deployment to your existing chat model instead of the sample `gpt-5-mini` value.

3. **Deploy:**

   ```powershell
   azd deploy chapter17-reasoning --no-prompt
   ```

4. **Check runtime inference access.** Read `instance_identity.principal_id`, resolve the actual model-hosting Foundry account ARM ID, and check effective assignments including inherited grants. If access is missing, assign **Cognitive Services OpenAI User** to the current runtime identity at that account scope only when authorized. The deploying identity separately needs permission to deploy hosted agents in the project.

   ```powershell
   azd ai agent show chapter17-reasoning --output json
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow for RBAC propagation, verify with a representative request, and recheck the runtime principal after redeployment.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter17-reasoning --protocol responses 'react: Calculate 10 factorial.'
   ```

Bundled .NET 10 code deployment publishes this host with its Chapter 17 and Shared project references; no Docker image or container registry is required.

## Telemetry and sensitive data

The hosted service emits MAF spans through the `Chapter17.Reasoning` activity source. **Sensitive-data capture is enabled for this demo.** Prompts, drafts, research notes, tool inputs, and responses may appear in traces. Use non-sensitive inputs, restrict trace access and retention, and disable sensitive capture before processing confidential information. For local traces, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter17-reasoning`.

## Costs and cleanup

Reasoning, review, and research use multiple model calls; hosted runtime and model use may incur charges. Fixed round/query bounds do not fix actual costs, which depend on prompt and response lengths. Remove only the `chapter17-reasoning` agent and experimental resources you own through Foundry's agent-scoped controls. Do not run `azd down` against a shared project.

## Pattern tradeoffs

Reasoning and reflection can produce plausible but incorrect results; reviewer agreement is not independent evidence, and model-knowledge research may be stale. Tool use and bounded loops add latency and cost, while limits do not guarantee correctness. Keep deterministic input checks and tool bounds authoritative, and use a live, source-backed search capability when current evidence is required.
