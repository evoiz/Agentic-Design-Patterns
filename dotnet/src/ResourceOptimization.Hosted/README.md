# Hosted Resource Optimization

Expose the [intent-routing pattern](../ResourceOptimization/README.md) through Foundry Responses. This companion keeps route/cost visibility and replaces console Google search with Foundry's native Bing-backed web search.

## Where to look in code

- [ResourceOptimizationAgent.cs](ResourceOptimizationAgent.cs): latest request, completed answer, and structured usage metadata. Its response/streaming overrides intentionally preserve usage beyond the shared text-only adapter.
- [Program.cs](Program.cs), `ResolveRouteDeployment`: route deployments and required native web-search tool.
- [ResourceOptimizationWorkflow.RunAsync](../ResourceOptimization/ResourceOptimizationWorkflow.cs): shared classify → optional search → answer sequence.

## Request contract

One nonempty `input` text, processed independently using only the latest user message. The completed response reports classification, deployment, actual answer, and provider token usage (including structured Responses usage for streaming). No history is persisted by the workflow; intermediate router output is not streamed.

`internet_search` uses the quality model through the project Responses API with required `web_search`. Citation annotation URLs are appended. Search/network failures propagate; there is no fallback to unsourced memory or Google. No Google API key, search-engine ID, Bing resource, or project connection is needed.

## Configuration and local commands

Use [shared settings](../../README.md) plus Entra credentials for project Responses access. An API key alone is insufficient for the web route. The quality deployment must support web search and subscription administrators must permit it.

| Optional alias | Route |
|---|---|
| `fast` | simple |
| `reasoning` | reasoning |
| `quality` | internet search |

Unset aliases fall back to the default model. If all aliases are unset, every route uses the default deployment and the sample demonstrates policy rather than cost reduction. Local configuration uses `Foundry:ModelDeployments:<alias>`; the azd environment exposes `FOUNDRY_MODEL_DEPLOYMENT_FAST`, `FOUNDRY_MODEL_DEPLOYMENT_REASONING`, and `FOUNDRY_MODEL_DEPLOYMENT_QUALITY`, falling back to `AZURE_AI_MODEL_DEPLOYMENT_NAME`.

From the repository root:

```powershell
dotnet run --project dotnet\src\ResourceOptimization.Hosted
```

In another terminal, from this project's directory:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter16-resource-optimization --local --protocol responses "What is the capital of Australia?"
azd ai agent invoke chapter16-resource-optimization --local --protocol responses "What is the latest Australian Open schedule?"
```

## Deploy and operate

Complete [HOSTING.md](../../HOSTING.md) first: reader-owned project/model, managed identity, bundled dependencies, runtime inference RBAC, sensitive trace controls, charges, and scoped cleanup. Keep keys out of Azure configuration. Then, from this project's directory:

```powershell
azd deploy chapter16-resource-optimization --no-prompt
azd ai agent show chapter16-resource-optimization --output json
# Verify runtime inference access before the remote invocation.
azd ai agent invoke chapter16-resource-optimization --protocol responses "Explain why routing can reduce cost."
```

Classification adds a model call and may misroute; aliases do not guarantee price or quality. Web search incurs extra usage and is governed by [Bing grounding terms](https://www.microsoft.com/bing/apis/grounding-legal-enterprise) and the [Microsoft Privacy Statement](https://go.microsoft.com/fwlink/?LinkId=521839). The Microsoft Data Protection Addendum does not apply to data sent to Bing grounding, and data can cross compliance/geographic boundaries. Use non-sensitive inputs and review [administrator controls](https://learn.microsoft.com/azure/ai-foundry/agents/how-to/tools/web-search?view=foundry).
