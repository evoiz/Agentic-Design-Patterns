# Chapter → MAF → Foundry Mapping

| # | Family | Pattern | Notebooks (`chapter_notebooks/`) | Primary MAF primitive (C#) | Optional Foundry hook |
|---|---|---|---|---|---|
| 01 | `01-Flow` | Prompt Chaining | `Chapter_01_*` (Code, JSON) | `AgentWorkflowBuilder.BuildSequential`; `WorkflowBuilder` + custom `Executor` for validation gate | Tracing (App Insights), structured outputs |
| 02 | `01-Flow` | Routing | `Chapter_02_*` (ADK, LangGraph, OpenRouter) | `WorkflowBuilder` with conditional / switch edges; Handoff orchestration | Model router: SLM (Foundry Local) vs LLM deployment |
| 03 | `01-Flow` | Parallelization | `Chapter_03_*` | `AgentWorkflowBuilder.BuildConcurrent`; fan-out/fan-in edges | Multiple deployments in parallel |
| 04 | `02-ReasonAndImprove` | Reflection | `Chapter_04_*` | Generator + independent verifier executors in a bounded loop | Foundry evaluators as verifier |
| 05 | `03-KnowledgeAndAction` | Tool Use | `Chapter_05_*` | `AIFunctionFactory.Create`, `ChatClientAgent` tools | Foundry Agent Service tools (Bing grounding, Code Interpreter, File Search), Toolbox |
| 06 | `01-Flow` | Planning | `Chapter_06_*` | Planner executor + worker agents; Magentic orchestration | Deep research tool in Agent Service |
| 07 | `04-Collaboration` | Multi-Agent | `Chapter_07_*` (Sequential, Parallel, Loop, Coordinator, AgentTool, CrewAI) | Sequential / Concurrent / Handoff / GroupChat / Magentic; `agent.AsAIFunction()` for agent-as-tool | Hosted agents |
| 08 | `03-KnowledgeAndAction` | Memory | `Chapter_08_*` | `AgentSession` (thread), `AIContextProvider`, workflow shared state | Foundry conversations / persistent threads |
| 09 | `02-ReasonAndImprove` | Learning & Adaptation | `Chapter_09_*` | Eval → curated prompt/skill update loop (offline) | Foundry evaluation runs |
| 10 | `03-KnowledgeAndAction` | MCP | `Chapter_10_*` | `ModelContextProtocol` C# SDK client → tools as `AIFunction`; C# MCP server | Foundry MCP tool integration |
| 11 | `02-ReasonAndImprove` | Goal Setting & Monitoring | `Chapter_11_*` | Bounded loop with typed goal/progress state and budgets | Tracing metrics |
| 12 | `05-TrustAndOperations` | Exception Handling | `Chapter_12_*` | Executor retry/fallback, checkpoints | Fallback deployment |
| 13 | `04-Collaboration` | Human-in-the-Loop | `Chapter_13_*` | `ApprovalRequiredAIFunction`, `RequestPort`, `RequestInfoEvent` | — |
| 14 | `03-KnowledgeAndAction` | RAG | `Chapter_14_*` | Retrieval tool / `AIContextProvider` | Azure AI Search, Agent Service File Search |
| 15 | `04-Collaboration` | A2A | `Chapter_15_*` | MAF A2A hosting & client (`Microsoft.Agents.AI.A2A`) | Hosted agents as A2A endpoints |
| 16 | `05-TrustAndOperations` | Resource Optimization | `Chapter_16_*` | Router executor by complexity/budget; token usage from `UsageDetails` | SLM (Foundry Local) + LLM deployments |
| 17 | `02-ReasonAndImprove` | Reasoning | `Chapter_17_*` | ReAct via tool loop; reasoning model deployment; self-correction loop | o-series / reasoning deployments, Code Interpreter |
| 18 | `05-TrustAndOperations` | Guardrails | `Chapter_18_*` | Agent/function middleware, input/output validator executors | Azure AI Content Safety, Prompt Shields |
| 19 | `05-TrustAndOperations` | Evaluation | `Chapter_19_*` | LLM-as-judge agent, `Microsoft.Extensions.AI.Evaluation` | Foundry evaluations |
| 20 | `01-Flow` | Prioritization | `Chapter_20_*` | Agent proposes typed priority → deterministic scheduler | — |
| 21 | `02-ReasonAndImprove` | Exploration & Discovery | `Chapter_21_*` | Magentic / bounded exploration loop with budgets | Agent Service deep research |

Research sources: `ai-research/agentic-design-patterns-report-1.md` (sections by pattern number; also defines the five families), `ai-research/enterprise-agentic-design-patterns-2026-extension-report-2.md` (enterprise hardening). Family folders and their rationale: `docs/design-patterns.md`. A new pattern without a book chapter goes in the family whose guiding question it answers.

## Enterprise extensions

These are original patterns beyond the book chapters, not additional book chapters or notebook ports.

| ID | Family | Pattern | Research source | Primary MAF primitive (C#) | Foundry companion |
|---|---|---|---|---|---|
| E01 | `05-TrustAndOperations` | Agent Harness | `ai-research/agent-harness-pattern.md`; enterprise report section 1 | `HarnessAgent`, `HarnessAgentOptions`, `TodoProvider`, mode provider, bounded todo loop, compaction, tracing | `dotnet/src/05-TrustAndOperations/Extension01.AgentHarness.Hosted` exposes the same runner through Responses with Entra ID |
