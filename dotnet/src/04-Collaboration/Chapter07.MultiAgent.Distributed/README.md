# Chapter 07 - Hosted Distributed Crew

Expose the [Chapter 07 multi-agent workflow](../Chapter07.MultiAgent/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The distributed crew has separate researcher, writer, and orchestrator services. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter07-researcher`, `chapter07-writer`, and `chapter07-orchestrator` services.

## What it does

Send a topic as the latest user message to the orchestrator. The orchestrator calls the researcher for concise notes based on general model knowledge, then calls the writer with the topic and notes. The writer returns an article of about 350 words grounded in those notes. No live web search is performed and no sources are fetched.

- **Input:** non-empty plain text topic; no prefix or JSON wrapper is required.
- **Output:** the writer's final article, returned after both specialist calls complete.
- **Services:** each service runs the same executable with a different role. Researcher and writer call the model; the orchestrator has no model client and calls both specialists over HTTP.
- **State and isolation:** every request gets a fresh run and request-specific MAF session partition. The orchestrator does not reuse response IDs, conversation IDs, or specialist session IDs. The protocol may retain outer response history, but the application does not use it as cross-request memory.
- **Failures:** only completed Responses results containing assistant text are accepted. HTTP errors, incomplete responses, cancellation, and empty output propagate. Specialist requests have a three-minute timeout and are not automatically retried. Redirects are disabled. Remote endpoints must be HTTPS Foundry Responses URLs under `.services.ai.azure.com`, ending in `/responses`; loopback HTTP is accepted for local testing.
- **Identity:** outbound remote calls use an Entra token for `https://ai.azure.com/.default`. The official Foundry call-ID handler forwards `x-agent-foundry-call-id` when present; it does not forward a user ID or impersonate a caller. Foundry authorization is still required.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Explain three practical uses of AI agents in a product launch review.` | Orchestrator calls researcher, then writer with the notes. | An article around 350 words with three relevant themes, based on general knowledge and the supplied research notes. |
| `How can a small clinic use AI agents to improve appointment operations?` | Same sequential researcher-to-writer handoff for a different topic. | An accessible article grounded in the research notes; it should not claim live research or cite fabricated sources. |
| `Compare the benefits and risks of AI agents in a product launch.` | Specialists still execute in sequence; the writer receives the complete original topic and research notes. | A balanced article that preserves uncertainty and avoids unsupported claims. |
| ` ` (empty or whitespace-only message) | The request is rejected before specialist work. | A request validation error; no article is fabricated. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and Foundry project/model configuration. For local specialist model calls, configure the shared [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets); an API key can be used locally. Keep local listeners private.

From the repository root, start one process per role in three separate terminals:

```powershell
Set-Location .\dotnet\src\04-Collaboration\Chapter07.MultiAgent.Distributed
$env:Distributed__Role = 'researcher'
$env:PORT = '8091'
dotnet run
```

```powershell
Set-Location .\dotnet\src\04-Collaboration\Chapter07.MultiAgent.Distributed
$env:Distributed__Role = 'writer'
$env:PORT = '8092'
dotnet run
```

```powershell
Set-Location .\dotnet\src\04-Collaboration\Chapter07.MultiAgent.Distributed
$env:Distributed__Role = 'orchestrator'
$env:Distributed__ResearcherEndpoint = 'http://127.0.0.1:8091/responses'
$env:Distributed__WriterEndpoint = 'http://127.0.0.1:8092/responses'
$env:PORT = '8093'
dotnet run
```

In a fourth terminal, from the same project folder, post a Responses request to the orchestrator:

```powershell
Set-Location .\dotnet\src\04-Collaboration\Chapter07.MultiAgent.Distributed
Invoke-RestMethod -Uri 'http://127.0.0.1:8093/responses' `
    -Method Post -ContentType 'application/json' `
    -Body (@{ input = 'Explain three practical uses of AI agents in a product launch review.'; stream = $false } | ConvertTo-Json)
```

Stop all three processes with Ctrl+C. To run a single service through azd, start its required specialist dependencies first, then from this folder set the orchestrator endpoint variables and run it; the full three-process setup above remains the local end-to-end path:

```powershell
Set-Location .\dotnet\src\04-Collaboration\Chapter07.MultiAgent.Distributed
$env:CHAPTER07_RESEARCHER_ENDPOINT = 'http://127.0.0.1:8091/responses'
$env:CHAPTER07_WRITER_ENDPOINT = 'http://127.0.0.1:8092/responses'
azd ai agent run chapter07-orchestrator
# In another terminal, from this folder:
azd ai agent invoke chapter07-orchestrator --local --protocol responses 'Explain three practical uses of AI agents in a product launch review.'
azd ai agent invoke chapter07-orchestrator --local --protocol responses 'How can a small clinic use AI agents to improve appointment operations?'
```

## Deploy to Foundry

Follow the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents). Deploy the specialists first so their full Responses endpoints are available before deploying the orchestrator. Run azd commands from this project folder.

1. **Sign in and create an azd environment:**

   ```powershell
   Set-Location .\dotnet\src\04-Collaboration\Chapter07.MultiAgent.Distributed
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** For more about this configuration, see [point the manifest at your project and model](../../../README.md#point-the-manifest-at-your-project-and-model). Replace the sample `mvp-playground-foundry-proj` project key and endpoint with your Foundry project and update all three `uses` entries to that key. The researcher and writer read `AZURE_AI_MODEL_DEPLOYMENT_NAME`; set it to your chat-model deployment. The orchestrator reads the two endpoint values `CHAPTER07_RESEARCHER_ENDPOINT` and `CHAPTER07_WRITER_ENDPOINT` from the azd environment; set them after obtaining both specialist endpoints in step 3.

3. **Deploy researcher and writer first, then orchestrator:**

   ```powershell
   azd deploy chapter07-researcher --no-prompt
   azd deploy chapter07-writer --no-prompt
   azd ai agent show chapter07-researcher --output json
   azd ai agent show chapter07-writer --output json
   azd env set CHAPTER07_RESEARCHER_ENDPOINT '<returned-full-researcher-responses-url>'
   azd env set CHAPTER07_WRITER_ENDPOINT '<returned-full-writer-responses-url>'
   azd deploy chapter07-orchestrator --no-prompt
   azd ai agent show chapter07-orchestrator --output json
   ```

4. **Grant runtime access.** For each current `instance_identity.principal_id`, check effective assignments including inherited grants. Researcher and writer need **Cognitive Services OpenAI User** at the actual model-hosting Foundry account scope. The orchestrator needs **Foundry Agent Consumer** permission for both specialist agents, at individual agent scope when supported; project scope is broader and permits access to other agents in that project. Obtain authorization before changing RBAC, use the narrowest supported scope, and allow time for propagation. The caller also needs permission to invoke the orchestrator. Do not use the deploying user, project identity, or blueprint in place of a runtime principal. Re-resolve identities and recheck after redeployment.

   ```powershell
   azd ai agent show chapter07-researcher --output json
   azd ai agent show chapter07-writer --output json
   azd ai agent show chapter07-orchestrator --output json
   az role assignment create --assignee-object-id '<specialist-runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   az role assignment create --assignee-object-id '<orchestrator-runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Foundry Agent Consumer' --scope '<researcher-agent-or-approved-project-scope>'
   az role assignment create --assignee-object-id '<orchestrator-runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Foundry Agent Consumer' --scope '<writer-agent-or-approved-project-scope>'
   ```

5. **Invoke the orchestrator after RBAC propagation:**

   ```powershell
   azd ai agent invoke chapter07-orchestrator --protocol responses --new-session --new-conversation 'Explain three practical uses of AI agents in a product launch review.'
   ```

Bundled .NET 10 code deployment includes the distributed host and its referenced Chapter 07, Shared, and Chapter 07 hosted-companion assemblies. The companion assembly supplies shared configuration and isolation support; it does not start another host. No Docker image or registry is required.

## Distributed deployment and request isolation

The three services have separate Foundry agents, hosted processes, versions, and runtime identities. The orchestrator accepts only completed specialist output; errors and incomplete results are not converted into an article. Each request uses its own server-generated response ID for MAF session isolation, including managed-identity requests without a platform user header. The orchestrator sends Entra credentials only to remote Foundry endpoints, never to loopback HTTP. Resuming MAF state across responses is intentionally unsupported; send a fresh request rather than using `previous_response_id`.

## Telemetry and sensitive data

The `Chapter07.MultiAgent` activity source records specialist and orchestrator activity, including agent model-call spans. **Sensitive-data capture is enabled for this demo:** prompts, research notes, and article text may appear in traces. Restrict telemetry access and retention, and disable sensitive capture before handling confidential inputs. To view local spans, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter07-orchestrator` (or the specialist service being inspected).

## Costs and cleanup

This deployment runs three separately hosted services; a successful crew request makes two specialist model calls, plus hosted runtime and network charges. Remove only the reader-owned `chapter07-researcher`, `chapter07-writer`, and `chapter07-orchestrator` agents using Foundry's agent-scoped controls. Never run `azd down` against a shared project or remove shared models, projects, or unrelated agents.

## Pattern tradeoffs

Separate processes and identities make specialist boundaries and deployment lifecycles explicit, but introduce network latency, authentication, timeout, partial-failure, and per-service cost concerns. The orchestrator does not infer a local fallback if either specialist is unavailable. Use this pattern when independent deployment or access boundaries justify the added operational complexity.
