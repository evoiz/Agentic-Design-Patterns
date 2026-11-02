---
name: maf-pattern-demo
description: 'Port an Agentic Design Pattern chapter (Antonio Gulli book) from the Python notebooks in chapter_notebooks/ to a runnable C# .NET demo using Microsoft Agent Framework (MAF) and Microsoft Foundry, with a hosted-agent deployment companion when suitable. Use when: implement chapter, port notebook to C#, create MAF demo, add hosted deployment companion, prompt chaining / routing / parallelization / reflection / tool use / planning / multi-agent / memory / MCP / HITL / RAG / A2A / guardrails / evaluation pattern in .NET, add chapter README.'
argument-hint: 'Chapter number or pattern name, e.g. "01" or "Routing"'
---

# MAF Agentic Design Pattern Demo

Turns one book chapter into a self-contained C# console project inside the shared `dotnet/` solution, backed by a Microsoft Foundry model deployment, plus a chapter README and, when suitable, a separate hosted-agent deployment companion.

## When to Use
- "Implement Chapter NN with MAF / .NET"
- "Port the <pattern> notebook to C#"
- "Add a Foundry-backed demo for <pattern>"
- "Add a hosted-agent deployment companion for an implemented pattern"

## Inputs
- Chapter number (01–21) or pattern name. Resolve via [pattern mapping](references/pattern-mapping.md).

## Procedure

### 1. Research (read-only)
1. Read **all** notebooks matching `chapter_notebooks/Chapter_<NN>_*.ipynb`. Note: scenario, sample inputs, prompts, expected output, framework-specific tricks.
2. Read the matching sections in `ai-research/agentic-design-patterns-report-1.md` (pattern #NN: problem, risks, MAF mapping) and scan `ai-research/enterprise-agentic-design-patterns-2026-extension-report-2.md` for related enterprise patterns (Bounded Agentic Island, Evaluator-Contract, Safe-Action Gateway, etc.).
3. Check the row in [pattern mapping](references/pattern-mapping.md) for the MAF primitive and Foundry hook.
4. If MAF API details are uncertain, fetch current docs at `https://learn.microsoft.com/agent-framework/` (C# pivot). MAF packages are preview — **never guess signatures; verify via docs or build errors**.

### 2. Ensure shared solution exists (first run only)
If `dotnet/AgenticDesignPatterns.slnx` (or `.sln`) is missing, create it per [Foundry setup](references/foundry-setup.md):
- `dotnet/Directory.Build.props` — net10.0, nullable, implicit usings, **shared `UserSecretsId`**.
- `dotnet/Directory.Packages.props` — central package versions.
- `dotnet/src/Shared/` — `FoundrySettings` + `FoundryChatClientFactory` reading `secrets.json` via `Microsoft.Extensions.Configuration.UserSecrets`.
- `dotnet/README.md` — one-time secrets setup.

Auth:
- **Local execution**: Foundry project endpoint + API key stored in local `secrets.json` through .NET user-secrets.
- **Cloud-agent / GitHub Actions execution**: the same values come from GitHub Actions environment secrets mapped to `Foundry__*` environment variables.
Never commit secrets, never put keys in `appsettings.json`, never echo keys to console or chat.

### 3. Create the chapter project
Path: `dotnet/src/<Family>/Chapter<NN>.<PatternName>/` (e.g. `dotnet/src/01-Flow/Chapter01.PromptChaining`). Reference `Shared` (`..\..\Shared\Shared.csproj`), and add the project to the matching `/src/<Family>/` solution folder in `dotnet/AgenticDesignPatterns.slnx`. Keep project names and namespaces family-free.

Pick `<Family>` from the **Family** column in [pattern mapping](references/pattern-mapping.md); the taxonomy is explained in [`docs/design-patterns.md`](../../../docs/design-patterns.md):

| Folder | Family | Guiding question |
|---|---|---|
| `01-Flow` | Flow | How does work move through the system? |
| `02-ReasonAndImprove` | Reason & Improve | How does the agent think about and improve its work? |
| `03-KnowledgeAndAction` | Knowledge & Action | What does the agent know, and what can it do? |
| `04-Collaboration` | Collaboration | Who participates in the work? |
| `05-TrustAndOperations` | Trust & Operations | How does it stay safe, reliable, and affordable in production? |

Place test projects in the mirrored folder `dotnet/tests/<Family>/<Name>.Tests/` (solution folder `/tests/<Family>/`).

Structure:
```
Chapter<NN>.<PatternName>/
├── Chapter<NN>.<PatternName>.csproj
├── Program.cs              # menu/args: runs each demo variant
├── Demos/<Variant>Demo.cs  # one file per variant
├── Models/                 # typed records for structured outputs (if any)
└── README.md               # from ./assets/chapter-readme-template.md
```

For a single variant, keep its implementation at the project root rather than adding
a one-file `Demos/` directory. Existing variants and documented input formats must
remain available during simplification. Optimize for visible pattern code, not a LOC quota.

### 4. Implement demo variants

| Variant | Required | Purpose |
|---|---|---|
| **MAF + model deployment** | Always | Port of the notebook scenario (same inputs, prompts, expected output) using MAF and the Foundry model deployment via API key. One demo per distinct notebook idea; merge duplicates. |
| **Foundry-native** | Only if Foundry offers a distinct native capability for the pattern (e.g. tools, workflows, evaluations, File Search/RAG) | Same scenario implemented with that capability. Skip and explain why if Foundry adds no separate primitive. Hosting the existing MAF workflow is a deployment companion, not a native reimplementation. |

Use `ai-research` to shape prompts, naming and README content — not to add extra hardening variants.

Rules:
- Use the MAF primitive from the mapping table (e.g. `AgentWorkflowBuilder.BuildSequential`, `WorkflowBuilder` + executors, `ChatClientAgent`, `AIFunctionFactory`, `ApprovalRequiredAIFunction`).
- Agents are created from the shared `IChatClient`; one `ChatClientAgent` per responsibility with a clear `name`.
- Prefer structured output (`RunAsync<T>` / JSON schema) over "parse the string" when the notebook produces JSON.
- Deterministic code decides correctness (validation, routing rules, termination); the model decides judgment.
- **Prefer non-streaming APIs** for readability: `agent.RunAsync(...)` / `RunAsync<T>(...)` and `InProcessExecution.RunAsync(workflow, input)` then read `run.NewEvents` / `WorkflowOutputEvent`. Use streaming (`RunStreamingAsync`, `WatchStreamAsync`) only when the pattern requires it (e.g. HITL `RequestInfoEvent` loops, or no non-streaming API exists) and say why in a one-line comment.
- Print each step's intermediate output with a header so the pattern is visible when run.
- The MAF variant must run either with local `secrets.json` (endpoint, key, deployment) or with `Foundry__*` environment variables provided by GitHub Actions. The Foundry-native variant may require extra setup (e.g. Entra ID / `az login`, deployed hosted agent); document it in the README and fail with a clear message if not configured.
- For hosted agents, load the `microsoft-foundry` skill for scaffolding/deploy steps rather than improvising.
- No framework-irrelevant extras in console demos: no DI hosts, logging frameworks, or tests unless the pattern needs them. A separate hosted companion may add the web/protocol host and focused regression coverage required for hosting.
- Reuse `Shared.Hosting.StatelessWorkflowAgent` for stateless string-in/result-out hosted
  adapters and `HostedFoundryConfiguration` for common settings/model clients. Stateful memory,
  HITL, request isolation and identity boundaries need their existing explicit contracts.
- Remove compatibility streaming/event adapters only after a deterministic test proves the
  pinned MAF APIs preserve output, concurrency and failures without them.
- Prefer tiny typed sample tools and data to parsers, generic frameworks or presentation
  helpers. Do not remove cancellation, authorization, approval/replay checks, bounded execution
  or explicit errors to reduce line count. Keep intermediate pattern steps visible.
- Reuse `tests/TestSupport` scripted clients and Responses host fixtures. Retain pattern-policy,
  safety/isolation and one focused hosted smoke test per companion; centralize redundant
  protocol tests in `Shared.Tests` instead of duplicating them in every chapter.

### 5. Write the chapter README
Copy [README template](assets/chapter-readme-template.md) and fill **every** section: pattern summary + diagram (mermaid), notebook → C# mapping, setup/run, **when to use / when not**, 3–5 real-world scenarios, Foundry extensions, and design-pattern tradeoffs. Keep implementation scope and request behavior in their own sections rather than labeling them as limitations.

Lead with the pattern and add stable file/method links under **Where to look in code**.
Keep chapter guides concise; link common credentials/test setup to `dotnet/README.md`
and deployment/RBAC/packaging/cost/cleanup to `dotnet/HOSTING.md`. Companion guides
keep their service-specific commands and request/state/protocol contracts. Do not
repeat operational tutorials or add abstractions solely to achieve a line-count target.

Write public, reusable developer guides. Never add verification reports, deployment status/history, deployed version numbers/dates, environment-specific resource identifiers, invocation outcomes, or evaluation run results to READMEs. Use placeholders for the reader's project/model. Report execution outcomes and blockers in the task response, not repository documentation.

### 6. Validate
1. `dotnet build dotnet/AgenticDesignPatterns.slnx` — must be warning-free for new code.
2. **Local validation path**: if secrets are configured (`dotnet user-secrets list --project dotnet/src/Shared`), run `dotnet run --project dotnet/src/<Family>/Chapter<NN>.<PatternName>` and confirm each variant produces sensible output.
3. **Cloud-agent validation path**: if local secrets are not available, rely on the repository workflow `.github/workflows/live-foundry-tests.yml`. The workflow must run inside the protected `foundry-integration` environment and provide `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, `Foundry__ModelDeployment`, and any optional `Foundry__ModelDeployments__<name>` aliases from GitHub environment secrets.
4. When a cloud agent cannot run the live workflow directly because the workflow file is not yet active on the default branch, say so explicitly and tell the user to merge the workflow first or run it from the GitHub Actions UI after merge.
5. Add the chapter to its family table in `dotnet/README.md` and to the family index `dotnet/src/<Family>/README.md`.

### 7. Add a hosted-agent deployment companion when suitable

Perform this assessment at the end of every chapter implementation, independently of the Foundry-native variant decision.

1. **Assess suitability.** Can the existing MAF behavior run behind a supported hosted request protocol with deployable dependencies and an explicit state/interaction model? Ordinary request/response workflows are suitable. Interactive console input, human approvals, local-only dependencies, long-running work, and durable memory require a supported protocol/persistence design first; do not silently remove these behaviors. If unsuitable, document the concrete limitation and what adaptation is needed.
2. **Load `microsoft-foundry`.** Follow its current environment, official sample selection, re-hosting, authentication, packaging, deployment, and invocation guidance. Verify current API/package signatures instead of copying stale examples. Preserve existing framework/model choices.
3. **Create `dotnet/src/<Family>/Chapter<NN>.<PatternName>.Hosted/`** next to the chapter project. Add a separate C# hosted project to the solution. Extract reusable workflow/agent logic from the chapter instead of duplicating prompts or capturing console output. Process the caller's input rather than replaying sample batches. Keep console commands, intermediate output, and sample behavior intact.
4. **Wire the host.** Use the official Foundry hosting/protocol integration, propagate cancellation and errors, and return the actual final result. Define request isolation and conversation/persistence behavior explicitly. Support streaming only where the host/protocol requires it; buffering a completed workflow result is acceptable when documented. Hosted Azure execution uses Entra ID / managed identity, not committed API keys; keep the console user-secrets path unchanged.
5. **Add deployment artifacts.** Use azd .NET code deployment by default. Give each hosted project its own `azure.yaml` in the project folder (`project: .`, one service, or the related services of one deployable unit) so readers can deploy from that folder; copy the header, project reference, and `infra` block from an existing hosted project and add a `.agentignore`. Ensure the upload includes project references and central build/package files: either verify a complete remote-build context or publish all dependencies and use bundled deployment. Exclude `.azure`, environment files, user-secrets, and unnecessary build artifacts. Document required permissions, model/project settings, local invocation, deploy/invoke commands, costs, and safe agent-scoped cleanup.
6. **Validate the companion separately.** Build warning-free, verify console compatibility and routing/output behavior with focused deterministic checks where needed, verify publish/package contents, then invoke the local protocol endpoint. Existing model-only smoke tests do not prove hosted readiness.
7. **Deploy only with authorization.** Creating artifacts does not authorize Azure writes. Reuse the user's chosen project/model; otherwise obtain project selection and deployment consent before provisioning/deploying. Do not infer consent from Canvas's saved selection. Include the required runtime inference-role assignment in the deployment approval; if RBAC changes were not covered, obtain approval before assigning the role. Deploy only the selected service from its project folder.
8. **Ensure runtime model-inference access before invoking.** Deployment does not automatically grant inference permission. After every authorized deployment, resolve the current agent's `instance_identity.principal_id` from `azd ai agent show <service> --output json` and the actual model-hosting Foundry account ARM ID. Check effective role assignments, including inherited grants. When inference access is missing and authorized, assign **Cognitive Services OpenAI User** (`5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) to that runtime identity at the model-hosting account scope, following [runtime inference RBAC](references/foundry-setup.md#runtime-inference-rbac-required-for-hosted-deployment). Do not substitute the deploying user, project identity, or agent blueprint, broaden to subscription scope, or fall back to an API key. Recheck on redeployment because runtime identities can change. Allow for RBAC propagation, then verify an active version and a representative remote response; an active version alone is not success. If role assignment or inference remains blocked, report the blocker explicitly and do not claim completion.
9. **Document reusable usage.** Link the companion from the chapter, the family index README, and `dotnet/README.md`. Describe the request contract, local run commands, configuration, deploy/invoke commands, required inference role/scope, and general RBAC propagation guidance using placeholders. Keep resource-removal instructions scoped to reader-owned agents and separate from design-pattern tradeoffs. Describe supported scenarios without implementation-limitation sections. Report deployment/validation outcomes and blockers only in the task response, never in READMEs. Follow the Foundry deployment skill's post-deployment evaluation-suite decision without automatically running evaluations.

## Completion Checklist
- [ ] All chapter notebooks reviewed; each distinct idea has a MAF demo
- [ ] Foundry-native variant built, or README explains why it is not applicable
- [ ] Builds cleanly; validated locally with user-secrets or via the live GitHub workflow
- [ ] README has when-to-use / when-not / real-world scenarios
- [ ] No secrets in repo; console credentials come from user-secrets or GitHub environment secrets, hosted credentials from developer identity or managed identity
- [ ] Hosted companion added and locally verified when suitable, or concrete reason for skipping documented
- [ ] hosted version activated sensitive telemetry for debugging and tracing
- [ ] Hosted runtime uses managed identity; packaging contains referenced dependencies and excludes secrets
- [ ] If live deployment was requested, current runtime identity has Cognitive Services OpenAI User (or verified equivalent inference access) on the model-hosting account; missing access was assigned with authorization and propagation handled
- [ ] If live deployment was requested, active version and remote response verified, or blocker explicitly reported
