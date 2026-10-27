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
dotnet run --project dotnet/src/01-Flow/Chapter01.PromptChaining -- specs --deployment fast
dotnet run --project dotnet/src/01-Flow/Chapter01.PromptChaining -- all --deployment quality
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

## Pattern families

Projects are grouped into five pattern families under `src/<NN-Family>/` (tests mirror this under `tests/<NN-Family>/`). Project names and namespaces are unchanged. See [Agentic design patterns and families](../docs/design-patterns.md) for the taxonomy and when to use each family; each family folder has its own index README.

Rows marked **E01** are original enterprise extensions beyond the book's 21 chapters, and **Supplement** rows are additional demos; neither is a notebook port.

### [01 · Flow — how work moves](src/01-Flow/README.md)

| Chapter | Pattern | Project | README |
|---|---|---|---|
| 01 | Prompt Chaining | `src/01-Flow/Chapter01.PromptChaining` | [Chapter 01 guide](src/01-Flow/Chapter01.PromptChaining/README.md) |
| 02 | Routing | `src/01-Flow/Chapter02.Routing` | [Chapter 02 guide](src/01-Flow/Chapter02.Routing/README.md) |
| 03 | Parallelization | `src/01-Flow/Chapter03.Parallelization` | [Chapter 03 guide](src/01-Flow/Chapter03.Parallelization/README.md) |
| 06 | Planning | `src/01-Flow/Chapter06.Planning` | [Chapter 06 guide](src/01-Flow/Chapter06.Planning/README.md) |
| 20 | Prioritization | `src/01-Flow/Chapter20.Prioritization` | [Chapter 20 guide](src/01-Flow/Chapter20.Prioritization/README.md) |
| Supplement | Decision Models | `src/01-Flow/DecisionModels` | [Microsoft-Decision-1, OpenAI Decisions, and MAF guide](src/01-Flow/DecisionModels/README.md) |

### [02 · Reason & Improve — how quality rises](src/02-ReasonAndImprove/README.md)

| Chapter | Pattern | Project | README |
|---|---|---|---|
| 04 | Reflection | `src/02-ReasonAndImprove/Chapter04.Reflection` | [Chapter 04 guide](src/02-ReasonAndImprove/Chapter04.Reflection/README.md) |
| 09 | Learning & Adaptation | `src/02-ReasonAndImprove/Chapter09.LearningAdaptation` | [Chapter 09 guide](src/02-ReasonAndImprove/Chapter09.LearningAdaptation/README.md) |
| 11 | Goal Setting & Monitoring | `src/02-ReasonAndImprove/Chapter11.GoalSettingMonitoring` | [Chapter 11 guide](src/02-ReasonAndImprove/Chapter11.GoalSettingMonitoring/README.md) |
| 17 | Reasoning | `src/02-ReasonAndImprove/Chapter17.Reasoning` | [Chapter 17 guide](src/02-ReasonAndImprove/Chapter17.Reasoning/README.md) |
| 21 | Exploration & Discovery | `src/02-ReasonAndImprove/Chapter21.ExplorationDiscovery` | [Chapter 21 guide](src/02-ReasonAndImprove/Chapter21.ExplorationDiscovery/README.md) |

### [03 · Knowledge & Action — what the agent knows and can do](src/03-KnowledgeAndAction/README.md)

| Chapter | Pattern | Project | README |
|---|---|---|---|
| 05 | Tool Use | `src/03-KnowledgeAndAction/Chapter05.ToolUse` | [Chapter 05 guide](src/03-KnowledgeAndAction/Chapter05.ToolUse/README.md) |
| 08 | Memory | `src/03-KnowledgeAndAction/Chapter08.Memory` | [Chapter 08 guide](src/03-KnowledgeAndAction/Chapter08.Memory/README.md) |
| 10 | MCP | `src/03-KnowledgeAndAction/Chapter10.MCP` | [Chapter 10 guide](src/03-KnowledgeAndAction/Chapter10.MCP/README.md) |
| 14 | Retrieval-Augmented Generation (RAG) | `src/03-KnowledgeAndAction/Chapter14.RAG` | [Chapter 14 guide](src/03-KnowledgeAndAction/Chapter14.RAG/README.md) |

### [04 · Collaboration — who participates](src/04-Collaboration/README.md)

| Chapter | Pattern | Project | README |
|---|---|---|---|
| 07 | Multi-Agent Collaboration | `src/04-Collaboration/Chapter07.MultiAgent` | [Chapter 07 guide](src/04-Collaboration/Chapter07.MultiAgent/README.md) |
| 13 | Human-in-the-Loop | `src/04-Collaboration/Chapter13.HumanInTheLoop` | [Chapter 13 guide](src/04-Collaboration/Chapter13.HumanInTheLoop/README.md) |
| 15 | Inter-Agent Communication (A2A) | `src/04-Collaboration/Chapter15.A2A` | [Chapter 15 guide](src/04-Collaboration/Chapter15.A2A/README.md) |

### [05 · Trust & Operations — how it stays safe and production-ready](src/05-TrustAndOperations/README.md)

| Chapter | Pattern | Project | README |
|---|---|---|---|
| 12 | Exception Handling and Recovery | `src/05-TrustAndOperations/Chapter12.ExceptionHandling` | [Chapter 12 guide](src/05-TrustAndOperations/Chapter12.ExceptionHandling/README.md) |
| 16 | Resource Optimization | `src/05-TrustAndOperations/Chapter16.ResourceOptimization` | [Chapter 16 guide](src/05-TrustAndOperations/Chapter16.ResourceOptimization/README.md) |
| 18 | Guardrails | `src/05-TrustAndOperations/Chapter18.Guardrails` | [Chapter 18 guide](src/05-TrustAndOperations/Chapter18.Guardrails/README.md) |
| 19 | Evaluation & Monitoring | `src/05-TrustAndOperations/Chapter19.Evaluation` | [Chapter 19 guide and evaluation-tool comparison](src/05-TrustAndOperations/Chapter19.Evaluation/README.md) |
| E01 | Agent Harness | `src/05-TrustAndOperations/Extension01.AgentHarness` | [Pattern, MAF implementation, and combinations](src/05-TrustAndOperations/Extension01.AgentHarness/README.md) |

The decision-model supplement uses `ElBruno.AI.Decisions.Foundry` for both native decision APIs and composes typed judgments with MAF/MEAI specialists. It needs separate `Foundry:ModelDeployments:decision` (Microsoft-Decision-1) and `Foundry:ModelDeployments:openaiDecision` (gpt-6-luna) aliases; keep `Foundry:ModelDeployment` set to a generative chat model. See its guide for contracts, confidence/probability semantics, policy thresholds, and mapping to Routing, Prioritization, Guardrails, and Evaluation.

Agent Harness demonstrates the packaged MAF runtime with todos, bounded tool use, compaction, tracing, and deterministic completion checks. Its [research note](../ai-research/agent-harness-pattern.md) cites primary sources and out-of-the-box examples. Its `AgentHarnessSmokeTests` run through the existing protected live workflow after merge.

## Hosted-agent companions

Each hosted project exposes the matching MAF chapter logic through a Microsoft Foundry Agent Service protocol (Responses, Invocations for Chapter 13, A2A for Chapter 15). **Every hosted project is self-contained for deployment:** it has its own `azure.yaml`, so you can `cd` into the project folder and run, deploy, and invoke it without touching any other chapter. Each guide includes example prompts with the expected results.

| Family | Chapter | Hosted guide | Deploy from | Request |
|---|---|---|---|---|
| 01 Flow | 02 | [Routing](src/01-Flow/Chapter02.Routing.Hosted/README.md) | `src/01-Flow/Chapter02.Routing.Hosted` | Booking, information, or clarification |
| 01 Flow | 03 | [Parallelization](src/01-Flow/Chapter03.Parallelization.Hosted/README.md) | `src/01-Flow/Chapter03.Parallelization.Hosted` | `topic:` or `research:` |
| 01 Flow | 06 | [Planning](src/01-Flow/Chapter06.Planning.Hosted/README.md) | `src/01-Flow/Chapter06.Planning.Hosted` | Topic to plan and summarize |
| 01 Flow | 20 | [Prioritization](src/01-Flow/Chapter20.Prioritization.Hosted/README.md) | `src/01-Flow/Chapter20.Prioritization.Hosted` | One task message or a JSON `tasks` array |
| 01 Flow | Supplement | [Decision Models](src/01-Flow/DecisionModels.Hosted/README.md) | `src/01-Flow/DecisionModels.Hosted` | Text support request; typed decisions, deterministic review gates, and an optional specialist draft |
| 02 ReasonAndImprove | 04 | [Reflection](src/02-ReasonAndImprove/Chapter04.Reflection.Hosted/README.md) | `src/02-ReasonAndImprove/Chapter04.Reflection.Hosted` | `fact-check:`, `product:`, or `code-review:` |
| 02 ReasonAndImprove | 09 | [Learning & Adaptation](src/02-ReasonAndImprove/Chapter09.LearningAdaptation.Hosted/README.md) | `src/02-ReasonAndImprove/Chapter09.LearningAdaptation.Hosted` | Starting prompt instructions to evaluate and revise |
| 02 ReasonAndImprove | 11 | [Goal Setting & Monitoring](src/02-ReasonAndImprove/Chapter11.GoalSettingMonitoring.Hosted/README.md) | `src/02-ReasonAndImprove/Chapter11.GoalSettingMonitoring.Hosted` | Use case and optional JSON goals/iteration budget |
| 02 ReasonAndImprove | 17 | [Reasoning](src/02-ReasonAndImprove/Chapter17.Reasoning.Hosted/README.md) | `src/02-ReasonAndImprove/Chapter17.Reasoning.Hosted` | `cot:`, `self-correction:`, `react:`, or `deep-search:` request |
| 02 ReasonAndImprove | 21 | [Exploration & Discovery](src/02-ReasonAndImprove/Chapter21.ExplorationDiscovery.Hosted/README.md) | `src/02-ReasonAndImprove/Chapter21.ExplorationDiscovery.Hosted` | Research topic for a bounded proposal and reviewer panel |
| 03 KnowledgeAndAction | 05 | [Tool Use](src/03-KnowledgeAndAction/Chapter05.ToolUse.Hosted/README.md) | `src/03-KnowledgeAndAction/Chapter05.ToolUse.Hosted` | `search:`, `calculate:`, or `stock:` |
| 03 KnowledgeAndAction | 08 | [Memory](src/03-KnowledgeAndAction/Chapter08.Memory.Hosted/README.md) | `src/03-KnowledgeAndAction/Chapter08.Memory.Hosted` | Scripted managed-memory demo; synthetic facts and a fresh scope per request |
| 03 KnowledgeAndAction | 10 | [MCP](src/03-KnowledgeAndAction/Chapter10.MCP.Hosted/README.md) | `src/03-KnowledgeAndAction/Chapter10.MCP.Hosted` | Request to the configured remote MCP server |
| 03 KnowledgeAndAction | 14 | [Retrieval-Augmented Generation](src/03-KnowledgeAndAction/Chapter14.RAG.Hosted/README.md) | `src/03-KnowledgeAndAction/Chapter14.RAG.Hosted` | Plain-text question grounded in the sample corpus |
| 04 Collaboration | 07 | [Multi-Agent Collaboration](src/04-Collaboration/Chapter07.MultiAgent.Hosted/README.md) | `src/04-Collaboration/Chapter07.MultiAgent.Hosted` | `<scenario>: <task>` or a task for the default `crew` scenario |
| 04 Collaboration | 07 | [Distributed crew](src/04-Collaboration/Chapter07.MultiAgent.Distributed/README.md) | `src/04-Collaboration/Chapter07.MultiAgent.Distributed` (three services) | Topic sent to an orchestrator that calls separately hosted researcher and writer agents |
| 04 Collaboration | 13 | [Human-in-the-Loop](src/04-Collaboration/Chapter13.HumanInTheLoop.Hosted/README.md) | `src/04-Collaboration/Chapter13.HumanInTheLoop.Hosted` | Customer-support task followed by an approval decision in the same Invocations session |
| 04 Collaboration | 15 | [Inter-Agent Communication (A2A)](src/04-Collaboration/Chapter15.A2A.Hosted/README.md) | `src/04-Collaboration/Chapter15.A2A.Hosted` | Text request through A2A agent-card discovery, JSON-RPC, or HTTP+JSON streaming |
| 05 TrustAndOperations | 12 | [Exception Handling and Recovery](src/05-TrustAndOperations/Chapter12.ExceptionHandling.Hosted/README.md) | `src/05-TrustAndOperations/Chapter12.ExceptionHandling.Hosted` | Location address; bounded retry and area fallback |
| 05 TrustAndOperations | 16 | [Resource Optimization](src/05-TrustAndOperations/Chapter16.ResourceOptimization.Hosted/README.md) | `src/05-TrustAndOperations/Chapter16.ResourceOptimization.Hosted` | Request routed by intent; current-information requests use Foundry's Bing-backed `web_search` tool |
| 05 TrustAndOperations | 18 | [Guardrails](src/05-TrustAndOperations/Chapter18.Guardrails.Hosted/README.md) | `src/05-TrustAndOperations/Chapter18.Guardrails.Hosted` | Request screened before the guarded assistant responds |
| 05 TrustAndOperations | 19 | [Evaluation](src/05-TrustAndOperations/Chapter19.Evaluation.Hosted/README.md) | `src/05-TrustAndOperations/Chapter19.Evaluation.Hosted` | Legal survey question for a structured five-criterion review |
| 05 TrustAndOperations | E01 | [Agent Harness](src/05-TrustAndOperations/Extension01.AgentHarness.Hosted/README.md) | `src/05-TrustAndOperations/Extension01.AgentHarness.Hosted` | Self-contained question about synthetic checkout or identity service health; read-only recommendations and completion/evidence JSON |

### Configure and deploy hosted agents

Each chapter guide contains a quick-deploy section. This section explains the shared details behind it.

#### Tooling and Azure prerequisites

- .NET 10 SDK, PowerShell 7, Azure CLI, and Azure Developer CLI (azd).
- azd version 1.35.1 or later and the `azure.ai.agents` extension version 1.0.0-beta.19 or later, as declared in each project's `azure.yaml`.
- An existing Foundry project in a region that supports hosted agents, with hosting configured and a compatible chat-model deployment.
- Permission to deploy agents to the project and model-inference access for both your local developer identity and the hosted runtime identity.

Install the agent extension if needed:

```powershell
azd extension install azure.ai.agents
```

#### Per-project manifests and environments

Every hosted project folder contains an `azure.yaml` with exactly the service(s) for that project (`project: .`), a `host: azure.ai.project` reference, and `infra.provider: microsoft.foundry`. Run all `azd` commands from that project folder. azd stores its environment in the project's own `.azure` folder (ignored by Git), so every project has its own azd environment. Environments created in other folders are not reused automatically.

```powershell
Set-Location .\dotnet\src\01-Flow\Chapter02.Routing.Hosted
az login --tenant '<tenant-id>'
azd auth login --tenant-id '<tenant-id>'
azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
azd env set AZURE_TENANT_ID '<tenant-id>'
azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<existing-model-deployment>'
```

#### Point the manifest at your project and model

Each committed `azure.yaml` contains a concrete **sample** project reference (`mvp-playground-foundry-proj`) and a sample model (`gpt-5-mini`). **Replace both with your own values before deploying.** Rename the `host: azure.ai.project` service key, set its `endpoint` to your project endpoint, update the agent service's `uses` entry to the same key, and set the model deployment in the agent's `env` block:

```yaml
services:
    chapter02-routing:
        # Keep the other service settings unchanged.
        uses:
            - my-foundry-project
        env:
            AZURE_AI_MODEL_DEPLOYMENT_NAME: <existing-model-deployment>
    my-foundry-project:
        host: azure.ai.project
        endpoint: https://<account>.services.ai.azure.com/api/projects/<project>
```

Setting an azd environment variable alone does not replace a literal value in `azure.yaml`. Keep the project path, startup command, protocol, runtime, and bundled packaging settings. Do not run `azd init` over an existing manifest. Some projects need extra values (model aliases, MCP server URL, memory store, remote agent endpoints); their guides list them.

These guides target an existing project and model; they do not provision either. If hosting, regional availability, or permissions need setup, resolve those prerequisites with your Azure administrator.

#### Authentication and runtime permissions

Hosted startup reads the shared `Foundry` user-secrets and corresponding `Foundry__*` environment overrides. When canonical settings are absent, `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` provide runtime fallbacks. Make sure local secrets or overrides do not point at a different project or model. The Agent Harness companion always uses Entra ID and ignores any shared API key; its Azure runtime explicitly uses managed identity.

For local Entra ID authentication, leave `Foundry:ApiKey` blank or unset it, and remove any nonempty `Foundry__ApiKey` override. The console demos require an API key; if you remove a shared key for a hosted run, configure it again before running a console demo. Azure execution should use managed identity, not an API key. Azure CLI and azd sign-in are separate from model-client configuration.

Deploy the service from its project folder, then inspect it with `azd ai agent show <service> --output json`. Use **`instance_identity.principal_id`**, not the deploying user, project identity, or agent blueprint, when configuring runtime inference access:

1. Identify the actual model-hosting Foundry account ARM ID.
2. Check effective role assignments, including inherited grants, for the runtime principal.
3. If equivalent inference access is missing, have an authorized administrator assign **Cognitive Services OpenAI User** at that account scope.
4. Allow time for RBAC propagation before sending a remote request. Recheck the principal after redeployment because the runtime identity can change.

Example commands for inspecting and assigning access:

```powershell
az role assignment list --assignee-object-id '<runtime-principal-id>' --scope '<model-hosting-account-arm-id>' --include-inherited --fill-principal-name false --output table
# Run only if the role is missing and you are authorized to assign it.
az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
```

If invocation returns `401 PermissionDenied`, check the runtime principal, account scope, model endpoint, and role propagation before retrying. Do not broaden access to the subscription or switch to API keys to bypass missing runtime permissions.

#### Packaging, costs, and resource ownership

`dependencyResolution: bundled` makes the azd extension run `dotnet publish` on the hosted project locally before uploading. Publishing follows the project references, so the referenced chapter and Shared projects and the central `Directory.*.props` settings are included even though `azure.yaml` lives in the project folder. No Docker or container registry is required. Each project's `.agentignore` keeps `azure.yaml`, `.azure`, environment files, and build folders out of the upload. Keep user-secrets and connection strings out of published content and source control.

Hosting and model calls incur Azure charges, and each deployment creates an immutable agent version. When removing an experiment, use Foundry's agent-scoped management controls for the resources you own. Do not run `azd down` against a shared project or delete shared models, project infrastructure, or unrelated agents.

#### View local traces with Aspire Dashboard

Hosted companions register their chapter activity sources with the host's OpenTelemetry provider. `OTEL_EXPORTER_OTLP_ENDPOINT` selects an OTLP collector; `APPLICATIONINSIGHTS_CONNECTION_STRING` enables Azure Monitor export. To view local spans, start a standalone Aspire dashboard (requires the Aspire CLI):

```powershell
aspire dashboard run --frontend-url http://127.0.0.1:18888 --otlp-grpc-url http://127.0.0.1:4317 --otlp-http-url http://127.0.0.1:4318
```

Open the login URL printed by Aspire and keep authentication enabled. See the [standalone dashboard guide](https://aspire.dev/dashboard/standalone/). In a second terminal, from the hosted project folder, set the exporter before starting the agent:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://127.0.0.1:4317'
$env:OTEL_EXPORTER_OTLP_PROTOCOL = 'grpc'
$env:OTEL_SERVICE_NAME = '<service>-local'
azd ai agent run <service>
```

Send a request, then open **Traces** at `http://127.0.0.1:18888`. Allow a few seconds for batched export. Keep these loopback settings local rather than adding them to `azure.yaml` or azd environment state. The dashboard's default in-memory history is lost when it stops.

**Sensitive-data capture is enabled in these demos.** Prompts and responses may appear in traces. Use non-sensitive sample data, restrict telemetry access and retention, and disable sensitive capture before handling confidential data.
