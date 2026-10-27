# Chapter 15 - Hosted A2A Agent

Expose the [Chapter 15 inter-agent communication demo](../Chapter15.A2A/README.md) through Microsoft Foundry Agent Service's **A2A protocol**. The companion publishes an agent card and accepts JSON-RPC and HTTP+JSON requests; this folder is self-contained with its own [`azure.yaml`](azure.yaml) defining the `chapter15-a2a` service.

## What it does

- **Input:** A2A task messages containing `text/plain` user text. The agent card advertises the `weather-guidance` skill and both HTTP+JSON and JSON-RPC bindings at `/.well-known/agent-card.json`.
- **Output:** An A2A text message for a synchronous request, or task updates for HTTP+JSON streaming requests.
- **State:** Each request creates a fresh, non-persistent agent run; no conversation history is retained between requests.
- **Scope:** Weather, exchange-rate, and calendar requests are demonstrations only. The agent has no live weather, exchange-rate, or calendar integration and must not invent current observations, rates, appointments, or completed actions.
- **Local access:** The local listener does not authenticate callers and AgentHost binds to all interfaces. Run it only on a trusted machine/network and use firewall controls as needed. Foundry-hosted caller access is controlled by project/agent permissions.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| JSON-RPC task message: `What is the exchange rate from USD to EUR?` | Discovers the card and sends a synchronous JSON-RPC task. | A text response explains that a current exchange rate needs a live source; no rate is fabricated. |
| HTTP+JSON streaming task message: `What's the weather in Paris?` | Sends a streaming request using the HTTP+JSON binding. | Task updates provide general guidance and distinguish it from a live forecast. |
| HTTP+JSON streaming task message: `What's the exchange rate for JPY to GBP today?` | Streams a request for current information. | The response states that current rates are unavailable rather than presenting an invented rate. |
| JSON-RPC task message: `Schedule a calendar appointment for tomorrow at 9.` | Sends an unsupported side-effect request synchronously. | The response says a calendar integration is needed and does not claim an appointment was made. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and shared Foundry settings ([configure Foundry user-secrets](../../../README.md#configure-foundry-user-secrets)). A blank `Foundry:ApiKey` uses your developer identity through `DefaultAzureCredential`.

From the repository root:

```powershell
Set-Location .\dotnet\src\04-Collaboration\Chapter15.A2A.Hosted
$env:PORT = '5090'
$env:A2A_PUBLIC_URL = 'http://localhost:5090/'
azd ai agent run chapter15-a2a
# or, without azd:
# dotnet run --project .\Chapter15.A2A.Hosted.csproj
```

In a second terminal, from the same folder, invoke through azd:

```powershell
azd ai agent invoke chapter15-a2a --local --protocol a2a --new-session --new-conversation "What's the weather in Paris?"
azd ai agent invoke chapter15-a2a --local --protocol a2a --new-session --new-conversation "What is the exchange rate from USD to EUR?"
```

The Chapter 15 console client also discovers the local card and demonstrates the original JSON-RPC and HTTP+JSON streaming flows. Set the same `PORT` and `A2A_PUBLIC_URL` values in the server terminal, then run from the hosted project folder:

```powershell
dotnet run --project ..\Chapter15.A2A -- all http://localhost:5090/
# Or choose card, sync, or stream; an optional request replaces that mode's sample:
dotnet run --project ..\Chapter15.A2A -- sync http://localhost:5090/ "Explain why exchange rates need a current source."
dotnet run --project ..\Chapter15.A2A -- stream http://localhost:5090/ "What's the weather in Paris?"
```

The client's `all` mode discovers the agent card, sends the USD/EUR sample synchronously over JSON-RPC, and streams the JPY/GBP sample over HTTP+JSON. `PORT` defaults to `8088`; if `A2A_PUBLIC_URL` is unset, the local card uses that port. Keep the endpoint private and stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for background. Run azd commands in this project folder.

1. **Sign in and create an azd environment:**

   ```powershell
   Set-Location .\dotnet\src\04-Collaboration\Chapter15.A2A.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** Replace the sample `mvp-playground-foundry-proj` key and endpoint with your project, and update the service's `uses` entry. Set `AZURE_AI_MODEL_DEPLOYMENT_NAME` to your existing compatible chat-model deployment instead of the sample `gpt-5-mini` value.

3. **Deploy:**

   ```powershell
   azd deploy chapter15-a2a --no-prompt
   ```

4. **Check runtime inference access.** Read `instance_identity.principal_id` and resolve the actual model-hosting Foundry account ARM ID. Check effective assignments, including inherited grants; if access is missing, assign **Cognitive Services OpenAI User** to the runtime identity at that account scope. The deploying identity needs permission to deploy hosted agents to the project; callers need permission to invoke the agent. Deployment and invocation permissions are separate.

   ```powershell
   azd ai agent show chapter15-a2a --output json
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Assign a missing role only when authorized. Allow time for RBAC propagation and recheck the current principal after redeployment.

5. **Invoke the deployed service:**

   ```powershell
   azd ai agent invoke chapter15-a2a --protocol a2a --new-session --new-conversation "What's the weather in Paris?"
   ```

   Use Foundry's managed A2A endpoint and card for deployed discovery. Do not expose the unauthenticated local server to untrusted networks.

Bundled .NET 10 code deployment publishes this host with its Chapter 15 and Shared project references; no Docker image or container registry is required.

## Agent card and protocol bindings

The card describes `WeatherBot`, the `weather-guidance` skill, `text/plain` input/output, and streaming capability. It advertises HTTP+JSON and JSON-RPC interfaces at the agent URL. The server maps the well-known card route and both A2A request bindings.

## Telemetry and sensitive data

The `Chapter15.A2A` activity source traces WeatherBot and its model calls. **Sensitive-data capture is enabled for this demo.** Prompts and responses may appear in traces; use non-sensitive inputs, restrict access and retention, and disable sensitive capture before handling confidential data. For local traces, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter15-a2a`.

## Costs and cleanup

Hosted runtime and model calls incur charges. Remove only the `chapter15-a2a` agent and experimental resources you own through Foundry's agent-scoped controls. Do not run `azd down` against a shared project or remove shared model deployments.

## Pattern tradeoffs

A2A provides interoperable discovery and messaging, not orchestration, authorization, trust, or shared memory; callers need explicit policies for each boundary. Remote requests add network latency and independent failure modes. Without connected domain tools, model answers are not authoritative for live weather, exchange rates, or calendar operations.
