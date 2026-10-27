# Chapter 16 - Hosted Resource Optimization Agent

Expose the [Chapter 16 resource-optimization workflow](../Chapter16.ResourceOptimization/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host classifies each caller request, selects a configured model deployment, and uses Foundry's built-in `web_search` tool for current information; this folder is self-contained with its own [`azure.yaml`](azure.yaml) defining the `chapter16-resource-optimization` service.

## What it does

- **Input:** Latest caller message as Responses `input` text; no prefix is required.
- **Routing:** Classifier selects `simple` for direct facts, `reasoning` for logic/math/multi-step inference, or `internet_search` for current or changing information. Invalid or unsupported classifier JSON fails the request.
- **Output:** Classification, selected model deployment, provider-reported token usage when available, and final answer. Search citation URLs are included when returned by Foundry. Token totals are also exposed as Responses usage metadata.
- **State:** Each request uses a fresh workflow and retains no conversation history. The completed result is buffered into a final response; intermediate routing decisions are not streamed.
- **Search:** The search route uses Foundry's Bing-backed `web_search` through the project Responses API. Search/network errors propagate; the agent does not fall back to model memory. No Google API key, search engine ID, Bing resource, or project connection is required by this host.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `What is the capital of Australia?` | Classifier should select `simple` and use the fast alias when configured. | Concise factual answer with route, resolved deployment, and reported usage. |
| `Solve this: a train travels 180 km in 2.5 hours. What is its average speed, and how do you derive it?` | Classifier should select `reasoning` and use the reasoning alias when configured. | Answer with concise explanation and route/deployment/usage metadata. |
| `What is the latest Australian Open men's singles champion?` | Classifier should select `internet_search` and invoke Foundry `web_search` with the quality alias when configured. | Current-information answer with source URLs when citation annotations are returned. |
| `Is the situation in my area safe right now?` | Freshness and missing location may lead to a search classification; the request is underspecified. | Route is classifier-dependent; search errors are returned rather than replaced with an unsupported model-memory answer. |

Route choices are model classifications, not deterministic guarantees. An unset route alias uses the configured default deployment.

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, a Foundry project and model deployment, and Azure CLI developer sign-in. Configure shared Foundry user-secrets ([setup](../../../README.md#configure-foundry-user-secrets)); the web-search route uses `DefaultAzureCredential`, so a model API key alone is insufficient. Local use also requires project access and permission for web search from your subscription administrator.

From the repository root:

```powershell
Set-Location .\dotnet\src\05-TrustAndOperations\Chapter16.ResourceOptimization.Hosted
azd ai agent run chapter16-resource-optimization
# or, without azd:
# dotnet run --project .\Chapter16.ResourceOptimization.Hosted.csproj
```

In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter16-resource-optimization --local --protocol responses 'What is the capital of Australia?'
azd ai agent invoke chapter16-resource-optimization --local --protocol responses 'Solve this: a train travels 180 km in 2.5 hours. What is its average speed, and how do you derive it?'
azd ai agent invoke chapter16-resource-optimization --local --protocol responses 'What is the latest Australian Open men''s singles champion?'
```

Optional local aliases can be configured in shared user-secrets; unset aliases fall back to `Foundry:ModelDeployment`:

```powershell
dotnet user-secrets set 'Foundry:ModelDeployments:fast' '<fast-deployment-name>' --project ..\Shared
dotnet user-secrets set 'Foundry:ModelDeployments:reasoning' '<reasoning-deployment-name>' --project ..\Shared
dotnet user-secrets set 'Foundry:ModelDeployments:quality' '<quality-deployment-name>' --project ..\Shared
```

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents). Run azd commands from this project folder.

1. **Sign in and create an azd environment:**

   ```powershell
   Set-Location .\dotnet\src\05-TrustAndOperations\Chapter16.ResourceOptimization.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<default-chat-model-deployment>'
   azd env set FOUNDRY_MODEL_DEPLOYMENT_FAST '<fast-deployment-name>'
   azd env set FOUNDRY_MODEL_DEPLOYMENT_REASONING '<reasoning-deployment-name>'
   azd env set FOUNDRY_MODEL_DEPLOYMENT_QUALITY '<quality-deployment-name>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** Replace the sample `mvp-playground-foundry-proj` key and endpoint with your project and update the service's `uses` entry. Replace the sample `gpt-5-mini` default with your existing chat-model deployment. Optional azd aliases map to `Foundry__ModelDeployments__fast`, `Foundry__ModelDeployments__reasoning`, and `Foundry__ModelDeployments__quality`; configure corresponding `FOUNDRY_MODEL_DEPLOYMENT_FAST`, `FOUNDRY_MODEL_DEPLOYMENT_REASONING`, and `FOUNDRY_MODEL_DEPLOYMENT_QUALITY` values to use route-specific deployments. Leave an alias unset to use the default.

3. **Deploy:**

   ```powershell
   azd deploy chapter16-resource-optimization --no-prompt
   ```

4. **Check runtime permissions.** Read the current `instance_identity.principal_id`, resolve the actual model-hosting Foundry account ARM ID, and check effective grants including inherited assignments. Ensure **Cognitive Services OpenAI User** is available at that account scope; assign a missing role to the runtime identity only when authorized. The deploying identity separately needs hosted-agent deployment permission. Foundry `web_search` must be permitted by the subscription administrator and available to the project/model; a model API key alone is insufficient for the project Responses API route.

   ```powershell
   azd ai agent show chapter16-resource-optimization --output json
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow for RBAC propagation and recheck the runtime identity after redeployment. Web search is subject to the [Grounding with Bing terms](https://www.microsoft.com/bing/apis/grounding-legal-enterprise) and [Microsoft Privacy Statement](https://go.microsoft.com/fwlink/?LinkId=521839). The Microsoft Data Protection Addendum does not apply to data sent to Bing grounding, and that data can leave compliance and geographic boundaries; use non-sensitive prompts and review administrator controls in [Foundry web search documentation](https://learn.microsoft.com/azure/ai-foundry/agents/how-to/tools/web-search?view=foundry).

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter16-resource-optimization --protocol responses 'What is the latest Australian Open men''s singles champion?'
   ```

Bundled .NET 10 code deployment publishes the host and referenced Chapter 16 and Shared projects; no Docker image or container registry is required.

## Model aliases and web search

`fast` serves `simple`, `reasoning` serves `reasoning`, and `quality` serves `internet_search`. Missing aliases resolve to the default deployment. Configure the quality route with a model supported for Foundry `web_search`. Search errors are not hidden by a fallback. The console chapter's Google Custom Search configuration is separate and is not used by this hosted service.

## Telemetry and sensitive data

MAF spans use the `Chapter16.ResourceOptimization` activity source. **Sensitive-data capture is enabled for this demo.** Prompts, search results, and responses may appear in traces when an OpenTelemetry listener is configured. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential data. For local traces, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter16-resource-optimization`.

## Costs and cleanup

Model calls, Foundry web search, and hosted runtime can incur charges; provider token usage is not a complete cost measure. Remove only the hosted agent and experimental resources you own through Foundry's agent-scoped controls. Do not run `azd down` against a shared project or remove shared deployments.

## Pattern tradeoffs

Classification adds a model call, latency, and a chance of selecting the wrong route. An alias identifies a deployment but does not guarantee price or quality. Search results can be stale or incomplete, and external search adds cost, availability, privacy, and data-boundary considerations; cite sources and keep budgets and authorization outside the model's route decision.
