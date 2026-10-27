# Chapter 08 - Hosted Foundry Memory

Expose the [Chapter 08 Foundry-managed memory demo](../Chapter08.Memory/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter08-memory` service.

## What it does

Any non-empty caller message starts the same scripted demonstration: the agent stores the synthetic fact “Sam prefers window seats,” waits for memory extraction, creates a new MAF session, recalls the travel preference, then deletes the temporary scope's stored memories after a successful run. Caller text is only used to satisfy the request contract; it is not sent to the inner model or stored as a memory.

- **Input:** any non-empty plain-text user message; no prefix or JSON wrapper is required. Empty or whitespace-only text is rejected.
- **Output:** a transcript showing the synthetic store, memory-update wait, recall in a new session, and cleanup steps.
- **State and isolation:** each invocation creates a fresh scope, even if `Foundry:MemoryScope` is configured. The memory is shared only between the two inner sessions in that request, not between callers or invocations. The outer Responses protocol may retain its own request/response history; the app does not pass that history to the inner model or memory provider.
- **Cleanup and failures:** temporary-scope memories are deleted after a successful run. An interrupted or failed run may leave synthetic memories in that isolated scope; use store retention or administrative deletion to clean them up. Memory extraction is asynchronous and can make requests take longer than ordinary chat.
- **Purpose:** this is a pattern demonstration, not a personal-memory chatbot. A production agent must map authenticated callers to authorized durable scopes and define consent, retention, correction, and deletion policies; a raw request header or model-selected user ID is not sufficient authorization.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Run the memory demo` | Runs the fixed synthetic store-and-recall flow. | Transcript shows Sam's window-seat preference stored and recalled in a new session, followed by temporary-scope cleanup. |
| `Please show how memory works for a travel assistant.` | Same scripted flow; the caller's wording does not alter the sample fact. | Same transcript structure, not a personalized answer based on the caller's message. |
| `My name is Alex and I prefer aisle seats. Remember this.` | Caller details are not passed to the inner agent or written to memory. | Demonstration still stores and recalls only its synthetic Sam/window-seat fact. |
| ` ` (empty or whitespace-only message) | Request validation rejects the missing input. | No memory run is started. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, a Foundry project, a chat-model deployment, an embedding-model deployment, and developer access to the Foundry memory capability. Configure the shared [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets); the memory demo uses `DefaultAzureCredential` for the project client, so sign in with an identity authorized for the required memory operations. Keep the local endpoint private.

From the repository root:

```powershell
Set-Location .\dotnet\src\03-KnowledgeAndAction\Chapter08.Memory.Hosted
azd ai agent run chapter08-memory
# or run the host directly:
dotnet run
```

In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter08-memory --local --protocol responses 'Run the memory demo'
azd ai agent invoke chapter08-memory --local --protocol responses 'My name is Alex and I prefer aisle seats. Remember this.'
```

For direct local execution, configure the project endpoint, chat deployment, embedding deployment, and memory-store name as `Foundry:ProjectEndpoint` (or `FOUNDRY_PROJECT_ENDPOINT`), `Foundry:ModelDeployment` (or `AZURE_AI_MODEL_DEPLOYMENT_NAME`), `Foundry:ModelDeployments:embedding` (or `AZURE_AI_EMBEDDING_DEPLOYMENT_NAME`), and `Foundry:MemoryStoreName` (or `AZURE_AI_MEMORY_STORE_ID`). The azd local service maps `AZURE_AI_PROJECT_ENDPOINT` from this folder's environment into the host, but the chat deployment, embedding deployment, and store ID come from the literal sample values in [`azure.yaml`](azure.yaml) (`gpt-5-mini`, `text-embedding-3-large`, `chapter08-hosted-memory`). Replace them as described in deployment step 2 before using `azd ai agent run`. Stop the local process with Ctrl+C.

## Deploy to Foundry

Follow the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents). This service needs both a chat-model deployment and an embedding-model deployment, plus an appropriately scoped memory store. Run all azd commands from this project folder.

1. **Sign in and create an azd environment:**

   ```powershell
   Set-Location .\dotnet\src\03-KnowledgeAndAction\Chapter08.Memory.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   azd env set AZURE_AI_EMBEDDING_DEPLOYMENT_NAME '<embedding-model-deployment>'
   azd env set AZURE_AI_MEMORY_STORE_ID '<reader-owned-memory-store-name>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** For more about this configuration, see [point the manifest at your project and model](../../../README.md#point-the-manifest-at-your-project-and-model). Replace the sample `mvp-playground-foundry-proj` project key and endpoint with your project and update `uses`. Change the service's chat, embedding, and memory-store `env` values to `${AZURE_AI_MODEL_DEPLOYMENT_NAME}`, `${AZURE_AI_EMBEDDING_DEPLOYMENT_NAME}`, and `${AZURE_AI_MEMORY_STORE_ID}` respectively. The azd environment values set in step 1 then supply your chat deployment, embedding deployment, and memory-store name; keep the project endpoint aligned as well.

3. **Deploy:**

   ```powershell
   azd deploy chapter08-memory --no-prompt
   ```

4. **Grant runtime permissions.** Run `azd ai agent show chapter08-memory --output json` and resolve the current `instance_identity.principal_id`. Check effective assignments, including inherited grants. The runtime needs **Cognitive Services OpenAI User** at the actual model-hosting Foundry account scope for inference and **Foundry User** at the containing Foundry account ARM scope for memory-store operations, following the [memory authorization guidance](https://learn.microsoft.com/azure/foundry/agents/how-to/memory-usage#authorization-and-permissions). The Foundry User role grants account-wide Foundry data access; use an account appropriate for this demo. Obtain approval before RBAC writes, allow propagation, and recheck the identity and assignments after redeployment.

   ```powershell
   azd ai agent show chapter08-memory --output json
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role '53ca6127-db72-4b80-b1b0-d745d6d5456d' --scope '<foundry-account-arm-id>'
   ```

5. **Invoke after RBAC propagation:**

   ```powershell
   azd ai agent invoke chapter08-memory --protocol responses 'Run the memory demo'
   ```

Bundled .NET 10 code deployment publishes the hosted project with its referenced Chapter 08 and Shared projects; no Docker image or container registry is required.

## Memory scope and permissions

The provider creates the configured memory store if it does not exist. Foundry memory and its MAF provider are preview/experimental features. The hosted service intentionally ignores a configured reusable `Foundry:MemoryScope` and creates an isolated generated scope for each invocation. Only its two internal sessions share that scope. Failed or cancelled runs may require administrative cleanup of the generated scope; do not treat it as a durable caller profile. The local developer identity must also have inference access at the model-hosting account and authorization to create/use the memory store when running locally.

## Telemetry and sensitive data

The hosted source does not declare a named Chapter 08 activity source; Foundry/MAF memory or model instrumentation may emit spans for the run. **Treat prompts, extracted memory facts, and responses as sensitive data.** Restrict telemetry access and retention, and disable sensitive-data capture before processing confidential information. To view local spans, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter08-memory`.

## Costs and cleanup

Memory-store processing, embedding and chat-model calls, and hosted compute incur Azure charges. Delete only memory stores and hosted agents/versions you own using agent- and resource-scoped controls. Do not run `azd down` against a shared project or remove shared model deployments or account resources.

## Pattern tradeoffs

Managed memory can make information available across agent sessions without putting it into the prompt history, but requires identity-aware scope ownership and explicit retention and deletion policies. This demo's temporary synthetic scope avoids cross-request personalization; it is deliberately not a substitute for an authorized, durable user-memory design. Memory extraction and retrieval add latency and service dependencies.
