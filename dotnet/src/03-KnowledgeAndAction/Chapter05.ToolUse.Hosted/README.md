# Chapter 05 - Hosted Tool Use Agent

Expose the [Chapter 05 function-tool agent](../Chapter05.ToolUse/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF selects and invokes application-defined tools; Foundry hosts the agent and model. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter05-tool-use` service.

## What it does

The agent can call a deterministic sample search, a bounded arithmetic parser, or a simulated stock-price lookup. Prefixes restrict the agent to the corresponding tool; unprefixed input makes all three available.

- **Input:** `search: <query>`, `calculate: <expression>`, or `stock: <ticker>`, case-insensitive. Unprefixed text uses the general tool-enabled agent. An empty recognized prefix is invalid.
- **Output:** the agent's final answer after any selected tool call; tool diagnostics are not returned as the response.
- **State:** each request creates a fresh tool-enabled agent with no persisted conversation history. The response is buffered until the run completes.
- **Data and errors:** search and stock values are sample data, not live information. The calculator accepts `+`, `-`, `*`, `/`, parentheses, decimals, and factorial for integer inputs 0–27; it does not execute general code. Tool errors, provider failures, and cancellation propagate rather than producing successful-looking placeholders.

The console chapter separately demonstrates Foundry-native web search, code interpreter, and file search; those are not enabled by this hosted function-tool companion.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `search: capital of France` | The search prefix exposes only the sample information lookup | The response identifies Paris as the capital, based on the sample index |
| `calculate: (5 + 7) * 3` | The calculator prefix exposes the bounded arithmetic parser | The calculation tool returns `36`, and the agent reports the computed result |
| `stock: AAPL` | The stock prefix exposes the simulated stock lookup | The response labels the sample price (178.15) as simulated, not a live quote |
| `What is the tallest mountain?` | Missing prefix exposes all three tools and the model can select search | The sample search result identifies Mount Everest as the tallest mountain above sea level |
| `calculate: 28!` | The calculator receives a factorial outside its supported 0–27 range | The tool rejects the expression; the error is not converted into a successful result |
| `stock: XYZ` | The stock tool receives an unsupported ticker | The lookup fails because no simulated price exists for that ticker |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and shared `Foundry` user-secrets ([setup](../../../README.md#configure-foundry-user-secrets)). A blank `Foundry:ApiKey` selects your developer identity through `DefaultAzureCredential`.

From the repository root:

```powershell
Set-Location .\dotnet\src\03-KnowledgeAndAction\Chapter05.ToolUse.Hosted
azd ai agent run chapter05-tool-use
# or, without azd:
dotnet run --project Chapter05.ToolUse.Hosted.csproj
```

The local server listens on `http://localhost:8088`; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter05-tool-use --local --protocol responses 'search: capital of France'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'calculate: (5 + 7) * 3'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'stock: AAPL'
```

Stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for background; all commands below run in this project folder.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\03-KnowledgeAndAction\Chapter05.ToolUse.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<model-deployment>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** Replace the sample `mvp-playground-foundry-proj` key and endpoint with your project and update `uses`. Replace the sample `gpt-5-mini` model deployment with your model deployment (or use `${AZURE_AI_MODEL_DEPLOYMENT_NAME}` and the environment value set above).

3. **Deploy:**

   ```powershell
   azd deploy chapter05-tool-use --no-prompt
   ```

4. **Grant the runtime identity model access.** Run `azd ai agent show chapter05-tool-use --output json`, read `instance_identity.principal_id`, and assign **Cognitive Services OpenAI User** at the model-hosting Foundry account scope if it is missing:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow a few minutes for RBAC propagation and recheck after redeployments.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter05-tool-use --protocol responses 'calculate: (5 + 7) * 3'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 05 and Shared projects. No Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter05.ToolUse` activity source records agent and tool-call spans. **Sensitive-data capture is enabled for this demo.** Prompts, tool arguments, and responses may appear in traces. Use non-sensitive sample data, and disable sensitive capture before handling confidential data. To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter05-tool-use`.

## Costs and cleanup

Hosting and model calls incur Azure charges. Tool calls use the local deterministic sample functions and do not connect to live search or stock services. Remove only the `chapter05-tool-use` agent you own through Foundry's agent management. Don't run `azd down` against a shared project.

## Pattern tradeoffs

Model-selected tool names and arguments are untrusted input. Validate them at the tool boundary, scope credentials narrowly, and require deterministic authorization and approval for consequential writes. Tool results are only as reliable and current as their data source. When replacing sample lookups with external services, define freshness, timeouts, error handling, and attribution explicitly; treat retrieved content as data, not instructions.
