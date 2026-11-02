# Agentic Design Patterns — .NET / Microsoft Agent Framework

Chapter demos use Microsoft Agent Framework (MAF) workflows and model deployments hosted in Microsoft Foundry. Shared configuration is stored outside the repository with .NET user-secrets.

## Prerequisites

- .NET 10 SDK
- A Microsoft Foundry project endpoint, deployed model name, and API key

## Configure Foundry user-secrets

Set the values locally; do not add them to `appsettings.json`, source files, or commits:

```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ApiKey" "<your-api-key>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<deployment-name>" --project dotnet/src/Shared
```

`ModelDeployment` is the default. Add optional, named deployments for per-run selection:

```powershell
dotnet user-secrets set "Foundry:ModelDeployments:fast" "<fast-deployment-name>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployments:reasoning" "<reasoning-deployment-name>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployments:quality" "<quality-deployment-name>" --project dotnet/src/Shared
```

All chapter projects use the same `UserSecretsId`. The corresponding environment variable overrides are `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, and `Foundry__ModelDeployment`; named deployments use `Foundry__ModelDeployments__<name>`.
For the OpenAI-compatible model API, the shared client derives `https://<resource>.openai.azure.com/openai/v1/` from a standard Foundry project endpoint on `<resource>.services.ai.azure.com`.

Chapter 16 can optionally route to `fast`, `reasoning`, and `quality` model deployment aliases. Its console demo supports Google Custom Search for current-information requests; configure `GoogleCustomSearch:ApiKey` and `GoogleCustomSearch:SearchEngineId` in user-secrets or use `GOOGLE_CUSTOM_SEARCH_API_KEY` and `GOOGLE_CSE_ID` environment variables. Keep these external-service credentials out of source control. The hosted companion instead uses Foundry's Bing-backed `web_search` tool with Azure credentials and needs no external search API key.

Select a configured name when running a chapter demo; omit it to use the default:

```powershell
dotnet run --project dotnet/src/Chapter01.PromptChaining -- specs --deployment fast
dotnet run --project dotnet/src/Chapter01.PromptChaining -- all --deployment quality
```

## GitHub Actions live smoke tests

Use two separate validation layers:

- **Default CI** (`.github/workflows/dotnet-ci.yml`) builds the solution and runs deterministic tests that do not require Foundry credentials.
- **Live Foundry smoke tests** (`.github/workflows/live-foundry-tests.yml`) are manual-only and use a protected GitHub Environment instead of local user-secrets.

Create a GitHub Environment named `foundry-integration` and add these secrets:

- `FOUNDRY_PROJECT_ENDPOINT`
- `FOUNDRY_API_KEY`
- `FOUNDRY_MODEL_DEPLOYMENT`
- Optional aliases such as `FOUNDRY_MODEL_DEPLOYMENT_FAST`, `FOUNDRY_MODEL_DEPLOYMENT_REASONING`, and `FOUNDRY_MODEL_DEPLOYMENT_QUALITY`

The live workflow maps those secrets onto the existing runtime environment variables:

- `Foundry__ProjectEndpoint`
- `Foundry__ApiKey`
- `Foundry__ModelDeployment`
- `Foundry__ModelDeployments__fast`
- `Foundry__ModelDeployments__reasoning`
- `Foundry__ModelDeployments__quality`

Chapter 06's plan-and-write, Chapter 08's session-memory, Chapter 09's prompt-adaptation, Chapter 11's goal-setting, Chapter 12's recovery, Chapter 14's local-corpus RAG workflows, and Chapter 19's notebook/MEAI evaluation demos are included in the live Foundry smoke tests. Chapter 06's optional Deep Research demo is not part of the smoke suite because it requires a separately deployed Deep Research model and runs as a potentially long background task. Chapter 08's Foundry-managed memory variant requires a configured embedding deployment and Entra ID, so it is an opt-in demo rather than part of the API-key smoke suite. Chapter 19's separate native cloud evaluation also requires Entra ID and creates evaluation resources, so it is run explicitly rather than by this API-key smoke suite.
Chapter 17's reasoning smoke test covers the four MAF/model-deployment demos. Its Foundry-native Web Search and Code Interpreter variants require Entra ID and are run separately.
Chapter 16's resource-optimization and Chapter 18's policy-guardrail workflows are also included in the live Foundry smoke tests.

That means the cloud agent and GitHub Actions jobs can invoke the chapter demos without `dotnet user-secrets`, as long as the workflow job runs inside the protected environment.

To run the live smoke tests manually:

1. Open **Actions** → **Live Foundry smoke tests**.
2. Start the workflow with an optional deployment alias if you want to target a named deployment.
3. The workflow builds the solution, runs deterministic tests, then runs live model smoke tests for Chapters 01–09, 11, 12, 14, 16, 17, 18, 19, and 21.

Any new test project that you add to `dotnet/AgenticDesignPatterns.slnx` is picked up by the non-live `dotnet test` step automatically. Live Foundry validation is intentionally narrower: the workflow checks out the default branch before loading protected secrets, so new demos only run there after you add coverage in `dotnet/tests/Foundry.SmokeTests` and merge that change to the default branch.

## Build

```powershell
dotnet build dotnet/AgenticDesignPatterns.slnx
```

## Chapter demos

| Chapter | Pattern | Project | README |
|---|---|---|---|
| 01 | Prompt Chaining | `src/Chapter01.PromptChaining` | [Chapter 01 guide](src/Chapter01.PromptChaining/README.md) |
| 02 | Routing | `src/Chapter02.Routing` | [Chapter 02 guide](src/Chapter02.Routing/README.md) |
| 03 | Parallelization | `src/Chapter03.Parallelization` | [Chapter 03 guide](src/Chapter03.Parallelization/README.md) |
| 04 | Reflection | `src/Chapter04.Reflection` | [Chapter 04 guide](src/Chapter04.Reflection/README.md) |
| 05 | Tool Use | `src/Chapter05.ToolUse` | [Chapter 05 guide](src/Chapter05.ToolUse/README.md) |
| 06 | Planning | `src/Chapter06.Planning` | [Chapter 06 guide](src/Chapter06.Planning/README.md) |
| 07 | Multi-Agent Collaboration | `src/Chapter07.MultiAgent` | [Chapter 07 guide](src/Chapter07.MultiAgent/README.md) |
| 08 | Memory | `src/Chapter08.Memory` | [Chapter 08 guide](src/Chapter08.Memory/README.md) |
| 09 | Learning & Adaptation | `src/Chapter09.LearningAdaptation` | [Chapter 09 guide](src/Chapter09.LearningAdaptation/README.md) |
| 10 | MCP | `src/Chapter10.MCP` | [Chapter 10 guide](src/Chapter10.MCP/README.md) |
| 11 | Goal Setting & Monitoring | `src/Chapter11.GoalSettingMonitoring` | [Chapter 11 guide](src/Chapter11.GoalSettingMonitoring/README.md) |
| 12 | Exception Handling and Recovery | `src/Chapter12.ExceptionHandling` | [Chapter 12 guide](src/Chapter12.ExceptionHandling/README.md) |
| 13 | Human-in-the-Loop | `src/Chapter13.HumanInTheLoop` | [Chapter 13 guide](src/Chapter13.HumanInTheLoop/README.md) |
| 14 | Retrieval-Augmented Generation (RAG) | `src/Chapter14.RAG` | [Chapter 14 guide](src/Chapter14.RAG/README.md) |
| 15 | Inter-Agent Communication (A2A) | `src/Chapter15.A2A` | [Chapter 15 guide](src/Chapter15.A2A/README.md) |
| 16 | Resource Optimization | `src/Chapter16.ResourceOptimization` | [Chapter 16 guide](src/Chapter16.ResourceOptimization/README.md) |
| 17 | Reasoning | `src/Chapter17.Reasoning` | [Chapter 17 guide](src/Chapter17.Reasoning/README.md) |
| 18 | Guardrails | `src/Chapter18.Guardrails` | [Chapter 18 guide](src/Chapter18.Guardrails/README.md) |
| 19 | Evaluation & Monitoring | `src/Chapter19.Evaluation` | [Chapter 19 guide and evaluation-tool comparison](src/Chapter19.Evaluation/README.md) |
| 20 | Prioritization | `src/Chapter20.Prioritization` | [Chapter 20 guide](src/Chapter20.Prioritization/README.md) |
| 21 | Exploration & Discovery | `src/Chapter21.ExplorationDiscovery` | [Chapter 21 guide](src/Chapter21.ExplorationDiscovery/README.md) |

The decision-model supplement uses `ElBruno.AI.Decisions.Foundry` for both native decision APIs and composes typed judgments with MAF/MEAI specialists. It needs separate `Foundry:ModelDeployments:decision` (Microsoft-Decision-1) and `Foundry:ModelDeployments:openaiDecision` (gpt-6-luna) aliases; keep `Foundry:ModelDeployment` set to a generative chat model. See its guide for contracts, confidence/probability semantics, policy thresholds, and mapping to Routing, Prioritization, Guardrails, and Evaluation.

## Enterprise extension patterns

These are original additions beyond the book's 21 chapters, not notebook ports or additional book chapters.

| Extension | Pattern | Project | Guide |
|---|---|---|---|
| E01 | Agent Harness | `src/Extension01.AgentHarness` | [Pattern, MAF implementation, and combinations](src/Extension01.AgentHarness/README.md) |
| E02 | Decision Models | `src/Extension02.DecisionModels` | [Bounded judgments, native providers, and MAF routing](src/Extension02.DecisionModels/README.md) |

Agent Harness demonstrates the packaged MAF runtime with todos, bounded tool use, compaction, tracing, and deterministic completion checks. Its [research note](../ai-research/agent-harness-pattern.md) cites primary sources and out-of-the-box examples. Its `AgentHarnessSmokeTests` run through the existing protected live workflow after merge.

For proposed additions, see the [agentic patterns backlog](../ai-research/agentic-patterns-backlog.md). It distinguishes existing coverage from 11 missing end-to-end contracts, with priorities, runnable C#/MAF scenarios, implementation todos, acceptance criteria, dependencies, and selective hosting guidance. Safe-Action Gateway, Durable Reactive Agent, and Governed Skill Packages are independent starting points; none of the backlog entries is implemented yet.

## Hosted-agent companions

Most hosted projects expose the same MAF chapter logic through Microsoft Foundry Agent Service protocols. Chapter 15 exposes its MAF agent through Foundry's **A2A protocol** and publishes an agent card. Use these companions as starting points for deploying the patterns to your own Foundry project.

| Chapter | Hosted guide | Request |
|---|---|---|
| 02 | [Routing](src/Chapter02.Routing.Hosted/README.md) | Booking, information, or clarification |
| 03 | [Parallelization](src/Chapter03.Parallelization.Hosted/README.md) | `topic:` or `research:` |
| 04 | [Reflection](src/Chapter04.Reflection.Hosted/README.md) | `fact-check:`, `product:`, or `code-review:` |
| 05 | [Tool Use](src/Chapter05.ToolUse.Hosted/README.md) | `search:`, `calculate:`, or `stock:` |
| 06 | [Planning](src/Chapter06.Planning.Hosted/README.md) | Topic to plan and summarize |
| 07 | [Multi-Agent Collaboration](src/Chapter07.MultiAgent.Hosted/README.md) | `<scenario>: <task>` or a task for the default `crew` scenario |
| 07 | [Distributed crew](src/Chapter07.MultiAgent.Distributed/README.md) | Topic sent to an orchestrator that calls separately hosted researcher and writer agents |
| 08 | [Memory](src/Chapter08.Memory.Hosted/README.md) | Caller recall question over synthetic facts in a fresh managed-memory scope per request |
| 09 | [Learning & Adaptation](src/Chapter09.LearningAdaptation.Hosted/README.md) | Starting prompt instructions to evaluate and revise |
| 10 | [MCP](src/Chapter10.MCP.Hosted/README.md) | Request to the configured remote MCP server |
| 11 | [Goal Setting & Monitoring](src/Chapter11.GoalSettingMonitoring.Hosted/README.md) | Use case and optional JSON goals/iteration budget |
| 12 | [Exception Handling and Recovery](src/Chapter12.ExceptionHandling.Hosted/README.md) | Location address; bounded retry and area fallback |
| 13 | [Human-in-the-Loop](src/Chapter13.HumanInTheLoop.Hosted/README.md) | Customer-support task followed by an approval decision in the same Invocations session |
| 14 | [Retrieval-Augmented Generation](src/Chapter14.RAG.Hosted/README.md) | Plain-text question grounded in the sample corpus |
| 15 | [Inter-Agent Communication (A2A)](src/Chapter15.A2A.Hosted/README.md) | Text request through A2A agent-card discovery, JSON-RPC, or HTTP+JSON streaming |
| 16 | [Resource Optimization](src/Chapter16.ResourceOptimization.Hosted/README.md) | Request routed by intent; current-information requests use Foundry's Bing-backed `web_search` tool |
| 17 | [Reasoning](src/Chapter17.Reasoning.Hosted/README.md) | `cot:`, `self-correction:`, `react:`, or `deep-search:` request |
| 18 | [Guardrails](src/Chapter18.Guardrails.Hosted/README.md) | Request screened before the guarded assistant responds |
| 19 | [Evaluation](src/Chapter19.Evaluation.Hosted/README.md) | Legal survey question for a structured five-criterion review |
| 20 | [Prioritization](src/Chapter20.Prioritization.Hosted/README.md) | One task message or a JSON `tasks` array |
| 21 | [Exploration & Discovery](src/Chapter21.ExplorationDiscovery.Hosted/README.md) | Research topic for a bounded proposal and reviewer panel |
| E01 | [Agent Harness](src/Extension01.AgentHarness.Hosted/README.md) | Self-contained question about synthetic checkout or identity service health; read-only recommendations and completion/evidence JSON |
| E02 | [Decision Models](src/Extension02.DecisionModels.Hosted/README.md) | Text support request; typed decisions, deterministic review gates, and an optional specialist draft |

### Configure and deploy hosted agents

See the [shared hosting guide](HOSTING.md) for tooling, project/model selection,
authentication, runtime RBAC, packaging, cost and safe cleanup. Each companion
README documents its own request/state contract and service-specific commands.

## Code and test ownership

| Surface | Responsibility | Validation |
|---|---|---|
| `src/Shared` | Console configuration and optional Deep Research API helper (Ch06/21) | `Shared.Tests`; Deep Research is opt-in |
| `src/Shared.Hosting` | Hosted configuration and stateless Responses adapter | `Shared.Tests` |
| Chapter/extension projects | Pattern decisions, tools and bounded orchestration | Corresponding deterministic test project |
| Hosted projects | Request parsing, state/isolation and protocol wiring | Companion smoke tests; approval/isolation regression tests |
| `tests/TestSupport` | Scripted models and in-process Responses host | Reused by deterministic suites |
| `tests/Foundry.SmokeTests` | Real model behavior | Explicit protected live workflow only |

```powershell
dotnet test dotnet\AgenticDesignPatterns.slnx --filter "Category!=LiveFoundry"
```

Default tests must not require credentials or perform Azure writes. Native evaluation,
Deep Research, managed memory and other Entra-dependent variants are opt-in and may
incur charges. See the chapter guides before invoking them.
