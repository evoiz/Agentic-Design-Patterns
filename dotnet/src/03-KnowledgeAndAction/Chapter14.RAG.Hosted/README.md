# Chapter 14 — Hosted RAG Agent

Expose the [Chapter 14 local-corpus RAG agent](../Chapter14.RAG/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host uses the same MAF retrieval tool and sample corpus as the console demo. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter14-rag` service.

## What it does

Send a plain-text question. Each request searches the bundled sample State of the Union corpus, retrieves up to five matching chunks, and returns one answer of no more than three sentences with source identifiers in square brackets. The sample corpus covers the 2022 address's remarks about Justice Breyer, the economy, and infrastructure; its notes paraphrase the address and link to the source transcript. If no relevant passage is found, the agent is instructed to say it does not know from the available documents.

Each request creates a fresh stateless run; caller conversation history is not persisted. Retrieval chunks text in 500-character segments with 50-character overlap and ranks matching terms deterministically. The Responses protocol can stream, but the host emits one completed update after retrieval and generation finish. The bundled corpus needs no vector store or external search connection.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `What did the president say about Justice Breyer?` | Searches the sample corpus for the Breyer address notes. | A concise answer grounded in the returned passage, citing `[SOTU-2022-BREYER]`. |
| `What did the address say about the economy?` | Retrieves matching economy passages. | A concise answer about recovery priorities such as jobs, domestic manufacturing, supply chains, and costs, with `[SOTU-2022-ECONOMY]` when that source is retrieved. |
| `What did the address say about infrastructure?` | Retrieves matching infrastructure passages. | A concise evidence-based answer about roads, bridges, broadband, or transportation networks, citing the source identifier. |
| `What did the address say about lunar volcanoes?` | Searches for a topic absent from the bundled corpus. | The agent says it does not know from the available documents rather than supplying unsupported facts. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and local Entra ID access or shared Foundry user-secrets; see [Configure Foundry user-secrets](../../../README.md#configure-foundry-user-secrets).

From the repository root:

```powershell
Set-Location .\dotnet\src\03-KnowledgeAndAction\Chapter14.RAG.Hosted
azd ai agent run chapter14-rag
# or, without azd:
dotnet run
```

The local server listens on port 8088; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter14-rag --local --protocol responses 'What did the president say about Justice Breyer?'
azd ai agent invoke chapter14-rag --local --protocol responses 'What did the address say about the economy?'
azd ai agent invoke chapter14-rag --local --protocol responses 'What did the address say about lunar volcanoes?'
```

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. If no API key is configured, it uses the signed-in developer identity locally. Stop the server with Ctrl+C.

## Deploy to Foundry

All commands run from this project folder. See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) and [project/model manifest guidance](../../../README.md#point-the-manifest-at-your-project-and-model) for setup details.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\03-KnowledgeAndAction\Chapter14.RAG.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<model-deployment>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** Replace the sample project key `mvp-playground-foundry-proj` and endpoint with your Foundry project, and update both the `uses` reference and project-service key. The service already reads `${AZURE_AI_MODEL_DEPLOYMENT_NAME}`; set that variable in step 1 to your chat-model deployment instead of the sample `gpt-5-mini` value.

3. **Deploy:**

   ```powershell
   azd deploy chapter14-rag --no-prompt
   ```

4. **Grant runtime model access.** Run `azd ai agent show chapter14-rag --output json` and read `instance_identity.principal_id`. Check effective role assignments, including inherited grants; if inference access is missing and authorized, assign **Cognitive Services OpenAI User** to that runtime identity at the actual model-hosting Foundry account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck the identity after redeployment.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter14-rag --protocol responses 'What did the president say about Justice Breyer?'
   ```

Bundled .NET 10 deployment publishes the host together with its referenced Chapter 14 and Shared projects; no Docker image or container registry is required.

## Telemetry and sensitive data

The MAF agent emits spans under the `Chapter14.RAG` activity source. **Sensitive-data capture is enabled:** caller questions, retrieved passages, and model responses may appear in configured traces. Use non-sensitive inputs, restrict trace access and retention, and disable sensitive capture before handling confidential data. For local traces, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter14-rag`.

## Costs and cleanup

Hosting and model inference incur Azure charges. The sample corpus is bundled, so this service does not provision a vector store or external search connection. Remove only the `chapter14-rag` agent and versions you own through Foundry's agent-scoped management controls. Do not run `azd down` against a shared project.

## Pattern tradeoffs

The bundled sample corpus and deterministic lexical ranking make retrieval reproducible and easy to inspect, but they are not a semantic-search service. Production RAG needs an approved, permission-aware and maintained index, plus evaluation of retrieval recall, source coverage, citation correctness, groundedness, latency, and cost. Retrieved text is evidence, not trusted instruction.
