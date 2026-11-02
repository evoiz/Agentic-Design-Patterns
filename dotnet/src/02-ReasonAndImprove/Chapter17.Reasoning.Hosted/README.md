# Chapter 17 — Hosted Reasoning

Run [all four MAF reasoning scenarios](../Chapter17.Reasoning/README.md) with caller input through Foundry Responses. Native Code Interpreter/Web Search remain opt-in console variants.

## Where to look in code

[ReasoningHostedAgent.cs](ReasoningHostedAgent.cs), `ParseRequest`: prefix dispatch; `RunAsync`: thin shared-adapter wrapper. [ReasoningWorkflow.cs](../Chapter17.Reasoning/ReasoningWorkflow.cs) owns scenario logic. [Program.cs](Program.cs) intentionally retains the specialized **Responses** inference adapter for reasoning/tool compatibility, rather than substituting a generic chat-completions client.

## Request contract

| Prefix | Behavior |
|---|---|
| `cot:` / `reason:` / unprefixed input | Direct answer plus concise rationale, never private chain-of-thought. |
| `self-correction:` / `selfcorrection:` | At most three draft/review rounds; deterministic 150-Unicode-text-element cap. |
| `react:` | Exact factorial tool for integers 0–20; no arbitrary local code execution. |
| `deep-search:` / `deepsearch:` | At most three queries per round, two rounds; model knowledge only, not browsing or verified citations. |

Only the latest user message is used. Recognized prefixes require nonempty content. Unknown prefixes remain ordinary question text. Each request has fresh state; Responses streaming buffers one completed result. Cancellation, invalid contracts, and model errors propagate. Review rejection returns its bounded outcome, not a false acceptance.

## Local commands

Configure [shared settings](../../../README.md). A local Development environment can use the configured API key; otherwise the host uses Entra credentials. Do not provide API keys to the Azure runtime.

```powershell
dotnet run --project dotnet\src\02-ReasonAndImprove\Chapter17.Reasoning.Hosted
```

In another terminal, from this project's directory:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter17-reasoning --local --protocol responses "cot: Explain quantum computing."
azd ai agent invoke chapter17-reasoning --local --protocol responses "self-correction: Write a short post about an eco-friendly product."
azd ai agent invoke chapter17-reasoning --local --protocol responses "react: Calculate 10 factorial."
azd ai agent invoke chapter17-reasoning --local --protocol responses "deep-search: Summarize batching from model knowledge."
```

## Deploy and operate

Follow [HOSTING.md](../../../HOSTING.md) for reader-owned project/model selection, bundled .NET packaging, managed identity/inference RBAC, sensitive tracing, cost, and scoped cleanup. After setup, from this project's directory:

```powershell
azd deploy chapter17-reasoning --no-prompt
azd ai agent show chapter17-reasoning --output json
# Verify runtime inference access before remote invocation.
azd ai agent invoke chapter17-reasoning --protocol responses "react: Calculate 10 factorial."
```

Self-review is not independent evidence. Reflection increases latency/cost and model-knowledge research can be stale. Use the separate Foundry Web Search console mode for current evidence; preserve tool range limits and deterministic acceptance rules when extending this host.
