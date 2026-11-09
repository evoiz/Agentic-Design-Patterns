# Agent Harness - Research and Implementation Mapping

**Classification:** runtime architecture / meta-pattern. This is an enterprise extension to the book taxonomy, not Chapter 22.

The [enterprise extension report](enterprise-agentic-design-patterns-2026-extension-report-2.md#1-agent-harness) already identifies Agent Harness as the operating environment around an LLM. The [runnable C# pattern guide](../dotnet/src/AgentHarness/README.md) turns that architectural description into a concrete implementation and includes when-to-use guidance, scenarios, tradeoffs, and combinations.

## What Is a Harness?

A model maps context to proposed text or tool calls. A harness repeatedly supplies context, interprets those proposals, executes permitted capabilities, records state, applies lifecycle policy, and determines what happens next. It makes the model useful as a worker inside an application rather than treating the model as the application.

Its responsibilities can include instructions, history, tools, planning/todos, context compaction, skills, memory, approvals, budgets, checkpoints, evaluation, tracing, and termination. These are a menu of responsibilities, not a requirement to enable every capability.

Distinguish three layers:

| Layer | Responsibility |
|---|---|
| Deterministic application/workflow | Business transitions, authentication, authorization, scheduling, transaction boundaries, and acceptance rules |
| Agent harness | Model/tool loop, context and task state, capability exposure, progress, budgets, and lifecycle |
| Model | Interpretation, reasoning, plans, proposals, and language generation |

A harness can host a planner-worker, a generator-verifier, a router, or a RAG agent. It is neither synonymous with multi-agent orchestration nor a security sandbox. Prompt instructions and execute/plan modes do not enforce permissions.

## Primary-Source Findings

| Source | Finding | Design consequence |
|---|---|---|
| [Microsoft Learn: Agent Harness](https://learn.microsoft.com/agent-framework/concepts/harness?pivots=programming-language-csharp) | MAF's packaged harness composes existing chat pipeline, context providers, and decorators, and remains an `AIAgent` | Use the actual `Microsoft.Agents.AI.Harness` package rather than a custom class merely named harness |
| [Pinned MAF `HarnessAgentOptions` source](https://github.com/microsoft/agent-framework/blob/dotnet-1.22.0/dotnet/src/Microsoft.Agents.AI.Harness/HarnessAgentOptions.cs) | Todo/mode providers, file memory, skills, web search, approval handling, tracing, optional looping, and compaction are individually configurable | Audit defaults; disable unwanted filesystem discovery and network capabilities explicitly |
| [Microsoft Learn: Planning and Todos](https://learn.microsoft.com/agent-framework/agents/planning-and-todos?pivots=programming-language-csharp) | Todos store progress in session state; an evaluator can continue work; plan/execute consent is instruction-level behavior | Use bounded loops and independent completion checks; never equate mode changes with authorization |
| [Anthropic: Effective harnesses for long-running agents](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents) | Compaction alone does not solve multi-session progress; initialization, incremental work, explicit progress artifacts, and verification address observed failures | Design checkpoint/handoff and acceptance evidence for long tasks rather than assuming a longer context solves them |
| [Foundry hosted-agent concepts](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents) | Hosting adds endpoint, identity, isolation, and platform lifecycle around custom agent code | Preserve application contracts when hosting; managed hosting is not a second reasoning-pattern implementation |

The repository report's broad readiness labels are architectural guidance, not a guarantee about every SDK feature or production workload. In the pinned MAF release, some loop/compaction APIs are experimental; package versions and capabilities must be checked together.

## Out-of-the-Box Examples

These are starting points, not interchangeable implementations:

| Example | Built-in composition / focus | Useful adaptation |
|---|---|---|
| [MAF Research Harness](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness/Harness_Step01_Research) | Research instructions, todos/modes, search/browsing, file memory, bounded completion loop, terminal UX | Evidence-based research with explicit context and memory policy |
| [MAF Research with Background Agents](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness/Harness_Step02_Research_WithBackgroundAgents) | Named background specialists inside the harness | Parallel evidence gathering with aggregate budgets and isolated child state |
| [MAF Data Processing](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness/Harness_Step03_DataProcessing) | Harness composition for data work | Artifact-producing analysis with scoped storage |
| [MAF Code Execution](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness/Harness_Step04_CodeExecution) | Harness composition with code execution | Add an actual execution sandbox and controlled promotion before using untrusted code |
| [MAF Loop](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness/Harness_Step05_Loop) | Evaluator-driven repeat execution | Stop on an explicit goal or report exhaustion; do not loop indefinitely |
| [Anthropic autonomous-coding quickstart](https://github.com/anthropics/claude-quickstarts/tree/main/autonomous-coding) | Initializer/coding-agent handoff, progress artifacts, feature acceptance tracking | Long-running coding work that survives context boundaries |

MAF's sample terminal console is sample application code, not a separate framework runtime. Foundry's **managed GitHub Copilot harness** is another product/runtime choice; it is not the MAF `HarnessAgent` or a synonym for hosting this C# code. Choose such a managed runtime explicitly based on its supported tools, state model, and governance requirements.

## Microsoft Agent Framework Implementation

The extension uses `IChatClient.AsHarnessAgent(HarnessAgentOptions)` backed by the shared Foundry model client:

- An explicit `TodoProvider` retains session-backed progress and exposes it to deterministic inspection.
- An execute-only mode avoids introducing a console approval conversation into a one-request hosting contract.
- Two allowlisted synthetic read tools keep the action boundary visible and side-effect-free.
- The native function loop is wrapped by a global model-call guard; todo-loop passes share the same guard.
- Input length, deadline, unfinished todos, and successful matching evidence receipts are checked in code.
- History and compaction belong to the request, not a shared user or tenant memory.
- Tracing surrounds the harness; confidential telemetry capture is a deliberate deployment setting.

The example intentionally does not provide production writes, a shell, directory-discovered skills, or durable memory. Those require authorization, isolation, persistence, and recovery designs appropriate to the workload. The [hosted companion](../dotnet/src/AgentHarness.Hosted/README.md) preserves that contract using Responses and managed identity.

## Pattern Composition Rules

Combine a harness with **planning** for progress, **RAG** for evidence, **reflection/evaluator-contract loops** for quality, **routing** for specialist/model selection, and **multi-agent/parallelization** for independent work. Add **HITL/Safe-Action Gateway** when side effects are consequential; add **durable recovery and governed memory** when work spans processes or sessions. **Guardrails and capability-scoped identity** enforce trust boundaries, while **EvalOps** governs changing models, prompts, tools, and skills.

Prefer a **Bounded Agentic Island** when the business process already has a deterministic skeleton: invoke the harness only at the judgment points. Never let child agents reset task budgets, let model-generated todos replace acceptance tests, or treat a successful deployment as proof of runtime inference access.

The [combination matrix](../dotnet/src/AgentHarness/README.md#combining-agent-harness-with-other-patterns) links these choices to the existing chapter implementations.
