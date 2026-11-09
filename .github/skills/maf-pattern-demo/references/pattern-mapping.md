# Chapter → MAF → Foundry Mapping

This is a lookup for the existing book chapters, not an exhaustive taxonomy or a
requirement for new demos. For external Python, papers or original contracts, use
the source tracks in [the skill](../SKILL.md). Choose a verified MAF primitive from
the actual mechanism; an absent row does not block implementation. The entries
below suggest building blocks, not guarantees that each console implements every
listed capability.

| # | Family | Pattern | Notebooks (`chapter_notebooks/`) | Primary MAF primitive (C#) | Optional Foundry hook |
|---|---|---|---|---|---|
| 01 | Flow | Prompt Chaining | `Chapter_01_*` (Code, JSON) | `AgentWorkflowBuilder.BuildSequential`; `WorkflowBuilder` + custom `Executor` for validation gate | Tracing (App Insights), structured outputs |
| 02 | Flow | Routing | `Chapter_02_*` (ADK, LangGraph, OpenRouter) | `WorkflowBuilder` with conditional / switch edges; Handoff orchestration | Model router: SLM (Foundry Local) vs LLM deployment |
| 03 | Flow | Parallelization | `Chapter_03_*` | `AgentWorkflowBuilder.BuildConcurrent`; fan-out/fan-in edges | Multiple deployments in parallel |
| 04 | Reason & Improve | Reflection | `Chapter_04_*` | Generator + independent verifier executors in a bounded loop | Foundry evaluators as verifier |
| 05 | Knowledge & Action | Tool Use | `Chapter_05_*` | `AIFunctionFactory.Create`, `ChatClientAgent` tools | Foundry Agent Service tools (Bing grounding, Code Interpreter, File Search), Toolbox |
| 06 | Flow | Planning | `Chapter_06_*` | Planner executor + worker agents; Magentic orchestration | Deep research tool in Agent Service |
| 07 | Collaboration | Multi-Agent | `Chapter_07_*` (Sequential, Parallel, Loop, Coordinator, AgentTool, CrewAI) | Sequential / Concurrent / Handoff / GroupChat / Magentic; `agent.AsAIFunction()` for agent-as-tool | Hosted agents |
| 08 | Knowledge & Action | Memory | `Chapter_08_*` | `AgentSession` (thread), `AIContextProvider`, workflow shared state | Foundry conversations / persistent threads |
| 09 | Reason & Improve | Learning & Adaptation | `Chapter_09_*` | Eval → curated prompt/skill update loop (offline) | Foundry evaluation runs |
| 10 | Knowledge & Action | MCP | `Chapter_10_*` | `ModelContextProtocol` C# SDK client → tools as `AIFunction`; C# MCP server | Foundry MCP tool integration |
| 11 | Reason & Improve | Goal Setting & Monitoring | `Chapter_11_*` | Bounded loop with typed goal/progress state and budgets | Tracing metrics |
| 12 | Trust & Operations | Exception Handling | `Chapter_12_*` | Executor retry/fallback, checkpoints | Fallback deployment |
| 13 | Collaboration | Human-in-the-Loop | `Chapter_13_*` | `ApprovalRequiredAIFunction`, `RequestPort`, `RequestInfoEvent` | — |
| 14 | Knowledge & Action | RAG | `Chapter_14_*` | Retrieval tool / `AIContextProvider` | Azure AI Search, Agent Service File Search |
| 15 | Collaboration | A2A | `Chapter_15_*` | MAF A2A hosting & client (`Microsoft.Agents.AI.A2A`) | Hosted agents as A2A endpoints |
| 16 | Trust & Operations | Resource Optimization | `Chapter_16_*` | Router executor by complexity/budget; token usage from `UsageDetails` | SLM (Foundry Local) + LLM deployments |
| 17 | Reason & Improve | Reasoning | `Chapter_17_*` | ReAct via tool loop; reasoning model deployment; self-correction loop | o-series / reasoning deployments, Code Interpreter |
| 18 | Trust & Operations | Guardrails | `Chapter_18_*` | Agent/function middleware, input/output validator executors | Azure AI Content Safety, Prompt Shields |
| 19 | Trust & Operations | Evaluation | `Chapter_19_*` | LLM-as-judge agent, `Microsoft.Extensions.AI.Evaluation` | Foundry evaluations |
| 20 | Flow | Prioritization | `Chapter_20_*` | Agent proposes typed priority → deterministic scheduler | — |
| 21 | Reason & Improve | Exploration & Discovery | `Chapter_21_*` | Magentic / bounded exploration loop with budgets | Agent Service deep research |

Research sources: `ai-research/agentic-design-patterns-report-1.md` (sections by pattern number; also defines the five families), `ai-research/enterprise-agentic-design-patterns-2026-extension-report-2.md` (enterprise contracts). The taxonomy is explained in `docs/design-patterns.md`. Choose family metadata by guiding question; projects remain flat under `dotnet\src\<PatternName>`. Do not assign fictional book chapters or require extension numbers for new patterns.

## Enterprise extensions

These are original patterns beyond the book chapters, not additional book chapters or notebook ports.

| ID | Family | Pattern | Research source | Primary MAF primitive (C#) | Foundry companion |
|---|---|---|---|---|---|
| E01 | Trust & Operations | Agent Harness | `ai-research/agent-harness-pattern.md`; enterprise report section 1 | `HarnessAgent`, `HarnessAgentOptions`, `TodoProvider`, mode provider, bounded todo loop, compaction, tracing | `dotnet/src/AgentHarness.Hosted` exposes the same runner through Responses with Entra ID |
