# Chapter 02 - Hosted Routing Agent

Expose the [Chapter 02 routing workflow](../Chapter02.Routing/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF provides the routing orchestration; Foundry hosts the agent and model. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter02-routing` service.

## What it does

A coordinator classifies each request as **BOOKER**, **INFO**, or **UNCLEAR**. The original request goes to the chosen specialist, or the workflow returns a fixed clarification message. Bookings are simulated; no reservations are made.

- **Input:** the latest user message (plain text). No prefix is required.
- **Output:** the specialist's final answer, without console headers or router diagnostics.
- **State:** each request is independent; no conversation history is persisted. Responses are buffered until the workflow completes.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Book me a hotel in Paris for next weekend.` | Coordinator returns BOOKER → Booker agent | Short confirmation that names Paris and the dates it extracted, and states that the booking was **simulated** |
| `Find me a flight from Zurich to New York on 12 March.` | BOOKER → Booker agent | Simulated flight-booking confirmation that mentions the Zurich → New York itinerary |
| `What is the highest mountain in the world?` | INFO → Info agent | Concise factual answer (Mount Everest, about 8,849 m) |
| `Can you help me with this?` | UNCLEAR → clarification executor (no specialist call) | The fixed text: *"Coordinator could not delegate the request confidently. Please clarify whether you need travel booking help or general information."* |

Ambiguous mixes such as `Tell me about Rome and book me something there` may route either way. That's a model judgment, as discussed in [Pattern tradeoffs](#pattern-tradeoffs).

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and the shared `Foundry` user-secrets ([setup](../../../README.md#configure-foundry-user-secrets)). A blank `Foundry:ApiKey` selects your developer identity through `DefaultAzureCredential`.

From the repository root:

```powershell
Set-Location .\dotnet\src\01-Flow\Chapter02.Routing.Hosted
azd ai agent run chapter02-routing
# or, without azd: dotnet run   (optional alias: dotnet run -- --deployment reasoning)
```

The server listens on `http://localhost:8088`; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter02-routing --local --protocol responses 'Book me a hotel in Paris for next weekend.'
azd ai agent invoke chapter02-routing --local --protocol responses 'What is the highest mountain in the world?'
azd ai agent invoke chapter02-routing --local --protocol responses 'Can you help me with this?'
```

Stop the server with Ctrl+C.

## Deploy to Foundry

Start from the repository root; all commands run in this folder. See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for background.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\01-Flow\Chapter02.Routing.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** Replace the sample `mvp-playground-foundry-proj` key and `endpoint` with your project (and update `uses`). Set `AZURE_AI_MODEL_DEPLOYMENT_NAME` to your chat-model deployment instead of `gpt-5-mini`.

3. **Deploy:**

   ```powershell
   azd deploy chapter02-routing --no-prompt
   ```

4. **Grant the runtime identity model access.** Read `instance_identity.principal_id`, then assign **Cognitive Services OpenAI User** on the model-hosting Foundry account if it is missing:

   ```powershell
   azd ai agent show chapter02-routing --output json
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow a few minutes for RBAC propagation. Recheck after redeployments.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter02-routing --protocol responses 'Book me a hotel in Paris for next weekend.'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 02 and Shared projects. No Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter02.Routing` activity source records `CoordinatorRouter` and the selected `Booker` or `Info` agent, with child model-call spans. UNCLEAR requests run only the coordinator.

**Sensitive-data capture is enabled for this demo.** Prompts, routing decisions, and responses may appear in traces. Use non-sensitive sample data, and disable sensitive capture before handling confidential data. To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter02-routing`.

## Costs and cleanup

Hosting and model calls incur Azure charges, and each deployment creates a new agent version. Remove only the `chapter02-routing` agent you own through Foundry's agent management. Don't run `azd down` against a shared project.

## Pattern tradeoffs

Routing decisions are model judgments and can misclassify ambiguous requests. Use explicit route contracts, a clarification path, and representative labeled examples when adapting the pattern to a new domain. Put authorization and consequential actions behind deterministic controls rather than trusting the selected route.
