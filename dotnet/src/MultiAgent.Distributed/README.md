# Distributed Foundry Crew (advanced/optional)

This second [multi-agent demo](../MultiAgent/README.md) deploys **three independently hosted Foundry agents**: a researcher, a writer, and an orchestrator. It uses the same crew prompts and task handoff as the single-host demo, but the specialists run across real HTTP boundaries. This is the Responses protocol, not A2A.

## Where to look in code

- [`Program`](Program.cs): role selection, model-free orchestrator, managed-identity specialists, and isolated Responses registration.
- [`RemoteSpecialist.InvokeAsync`](RemoteSpecialist.cs): endpoint allowlist, remote authentication, completion validation, and cancellation.
- [`StatelessRequestAgent`](StatelessRequestAgent.cs): thin shared `StatelessWorkflowAgent` adapter.
- [`MultiAgentWorkflow.RunCrewAsync`](../MultiAgent/CrewScenario.cs): the same explicit research → writing task handoff used locally; maps `Chapter_07_Multi_Agent_(CrewAI_Gemini).ipynb` into MAF responsibilities.
- [`RequestIsolationKeyProvider`](../MultiAgent.Hosted/RequestIsolationKeyProvider.cs): per-response partitioning, retained in both hosts.

## Request flow

The caller sends a topic to the orchestrator. It calls the research specialist, waits for completed research notes, then calls the writing specialist with the original topic and those notes. It returns the writer's actual final article. The orchestrator has no model client and never falls back to local model inference.

The research specialist works from general model knowledge, not live web search. The writer is instructed to ground the article in the supplied notes. All three services deploy the same executable with different `Distributed__Role` values; they are separate Foundry agents, hosted processes, versions, and runtime identities.

Each request starts a fresh run. No previous response ID, conversation ID, or specialist session ID is reused by the orchestrator. Foundry can store protocol response history, but the application does not use that history as cross-request memory. Results are buffered, and the orchestrator accepts only `completed` Responses output containing assistant text. HTTP errors, incomplete responses, cancellation, and empty outputs propagate instead of becoming success-shaped articles. Each specialist HTTP request has a three-minute timeout and is not automatically retried.

The distributed hosts explicitly partition MAF session state by the server-generated response ID, using `RequestIsolationKeyProvider`. This gives every request its own partition, including managed-identity calls with no platform user header. It does not impersonate an end user or bypass Foundry authorization. Resuming MAF session state across responses is intentionally unsupported; use a fresh request rather than `previous_response_id`.

## Configuration

The three services are defined in this directory's `azure.yaml`; deploy only the selected service.

| Service | Role | Configuration |
|---|---|---|
| `chapter07-researcher` | `researcher` | Foundry project endpoint and chat-model deployment |
| `chapter07-writer` | `writer` | Foundry project endpoint and chat-model deployment |
| `chapter07-orchestrator` | `orchestrator` | Full researcher and writer Responses URLs |

Set `Distributed__Role` to the role being run. The orchestrator requires `Distributed__ResearcherEndpoint` and `Distributed__WriterEndpoint`. Remote endpoints must be HTTPS URLs under `.services.ai.azure.com` ending in `/responses`, including the API-version query returned by Foundry. Loopback HTTP endpoints are accepted only to support local processes. Redirects are disabled to avoid forwarding credentials to a different destination.

Specialists read the same Foundry configuration as the single-host companion. Local development can use the shared console user-secrets; hosted execution always uses `DefaultAzureCredential` and the runtime's managed identity. The orchestrator obtains an Entra token for `https://ai.azure.com/.default` for remote calls; it sends no credentials to loopback URLs. The official `FoundryCallIdHandler` forwards the platform-minted `x-agent-foundry-call-id` on outbound calls so Foundry can resolve the originating caller's isolation context. It never forwards the user ID or fabricates an impersonation header; Entra authentication and agent-scoped authorization remain required.

## Run three processes locally

Configure the [shared Foundry credentials](../../README.md#configure-foundry-user-secrets), then build:

```powershell
dotnet build .\dotnet\src\MultiAgent.Distributed
dotnet test .\dotnet\tests\MultiAgent.Tests
```

In the first terminal, from the repository root:

```powershell
$env:Distributed__Role = 'researcher'
$env:PORT = '8091'
dotnet run --no-build --project .\dotnet\src\MultiAgent.Distributed
```

In a second terminal:

```powershell
$env:Distributed__Role = 'writer'
$env:PORT = '8092'
dotnet run --no-build --project .\dotnet\src\MultiAgent.Distributed
```

In a third terminal:

```powershell
$env:Distributed__Role = 'orchestrator'
$env:Distributed__ResearcherEndpoint = 'http://127.0.0.1:8091/responses'
$env:Distributed__WriterEndpoint = 'http://127.0.0.1:8092/responses'
$env:PORT = '8093'
dotnet run --no-build --project .\dotnet\src\MultiAgent.Distributed
```

From a fourth terminal, invoke the actual orchestration endpoint:

```powershell
Invoke-RestMethod -Uri 'http://127.0.0.1:8093/responses' `
    -Method Post -ContentType 'application/json' `
    -Body (@{ input = 'Explain three practical uses of AI agents in a product launch review.'; stream = $false } | ConvertTo-Json)
```

Stop all three processes with Ctrl+C. Keep local listeners private.

## Deploy to an existing Foundry project

Follow the [common hosting guide](../../HOSTING.md) first for configuration, project/model selection, bundled packaging, deployment identity, inference RBAC, and safe cleanup. Do not deploy against an unreviewed sample target. The orchestration-specific configuration and permissions below remain additional requirements.

From this directory, set the model deployment in its azd environment, then deploy only the selected service:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<existing-model-deployment>'
azd deploy chapter07-researcher --no-prompt
azd deploy chapter07-writer --no-prompt
azd ai agent show chapter07-researcher --output json
azd ai agent show chapter07-writer --output json
```

Copy each service's **returned full Responses endpoint** into the orchestrator configuration; do not infer or guess URLs:

```powershell
azd env set CHAPTER07_RESEARCHER_ENDPOINT '<returned-full-researcher-responses-url>'
azd env set CHAPTER07_WRITER_ENDPOINT '<returned-full-writer-responses-url>'
azd deploy chapter07-orchestrator --no-prompt
azd ai agent show chapter07-orchestrator --output json
```

Bundled deployment includes the distributed host, Chapter 07, Shared/Shared.Hosting, and the single-host companion assembly containing request-isolation support. Referencing that assembly does not start another host.

## Runtime permissions

Deployment rights and runtime access are separate. Resolve each **current `instance_identity.principal_id`** from `azd ai agent show`; never substitute the agent blueprint, project identity, or deploying user.

- The researcher and writer need **Cognitive Services OpenAI User** (or verified equivalent) at the actual model-hosting Foundry account scope.
- The orchestrator does not call the model. It needs **Foundry Agent Consumer** access for the two specialist agents, at the narrowest scope supported by the target's authorization model. Prefer individual agent scopes when supported. If project scope is required, explicitly review that wider boundary; it permits invocation of other agents in that project too.
- The caller needs permission to invoke the orchestrator.

Check effective/inherited role assignments before creating any grant, obtain authorization for RBAC writes, and allow propagation before invoking. Re-resolve runtime identities and recheck access after each redeployment. Never work around access errors with shared API keys or subscription-wide grants. See [Foundry RBAC](https://learn.microsoft.com/azure/foundry/concepts/rbac-foundry) for current role and scope support.

After permission propagation, verify an active version and an actual end-to-end remote response:

```powershell
azd ai agent invoke chapter07-orchestrator --protocol responses --new-session --new-conversation `
    'Explain three practical uses of AI agents in a product launch review.'
```

## Telemetry, cost, and cleanup

Sensitive prompt/response capture under `MultiAgent` is disabled by default. Keep telemetry access and retention scoped appropriately.

Use this design when specialists need independent identities, deployment lifecycles, or ownership. Example scenarios include a centrally operated research service feeding a separately owned publishing service, independently updated analysis specialists, and least-privilege departmental assistants. Prefer the single-host crew when those boundaries are unnecessary or latency/cost dominates.

This design runs three independently billed hosts and two model calls per successful crew request. Remote boundaries add authentication, cold-start/network latency, timeout, and partial-failure concerns compared with one host.

Delete only reader-owned experimental agents through Foundry's agent-scoped controls. Do not run `azd down` or delete shared projects, models, or unrelated agents. Evaluation-suite generation and evaluation execution are separate, optional operations requiring an explicit choice after deployment.
