# 03 · Knowledge & Action

> **Guiding question:** What does the agent know, and what can it do?

These patterns connect the model to the outside world: **grounding** it in data (RAG, memory) and letting it **act** on systems (tools, MCP). They turn a text generator into something that can read and change state.

Part of the five-family taxonomy described in [Agentic design patterns and families](../../../docs/design-patterns.md#3-knowledge--action). For setup, user-secrets, and hosted deployment, see the [.NET guide](../../README.md).

## Patterns

| Chapter | Pattern | Use when | Demo | Hosted companion | Tests |
|---|---|---|---|---|---|
| 05 | Tool Use | The model needs live data or must perform actions through typed functions. | [Chapter05.ToolUse](Chapter05.ToolUse/README.md) | [Chapter05.ToolUse.Hosted](Chapter05.ToolUse.Hosted/README.md) | [`ToolUse.Tests`](../../tests/03-KnowledgeAndAction/ToolUse.Tests) |
| 08 | Memory | Context must persist across turns or sessions (short-term history, long-term facts). | [Chapter08.Memory](Chapter08.Memory/README.md) | [Chapter08.Memory.Hosted](Chapter08.Memory.Hosted/README.md) | — |
| 10 | MCP | Tools should be exposed or consumed through a standard protocol across agents and hosts. Includes `Chapter10.MCP.Server`. | [Chapter10.MCP](Chapter10.MCP/README.md) | [Chapter10.MCP.Hosted](Chapter10.MCP.Hosted/README.md) | [`MCP.Tests`](../../tests/03-KnowledgeAndAction/MCP.Tests) |
| 14 | Retrieval-Augmented Generation (RAG) | Answers must be grounded in a document corpus with citations. | [Chapter14.RAG](Chapter14.RAG/README.md) | [Chapter14.RAG.Hosted](Chapter14.RAG.Hosted/README.md) | [`RAG.Tests`](../../tests/03-KnowledgeAndAction/RAG.Tests) |

## Design guidance

Prefer narrow, typed tools with explicit limits. Every tool or data source widens the attack surface, so pair this family with Guardrails and Human-in-the-Loop approvals for side-effecting actions.

## Run the tests for this family

```powershell
dotnet test dotnet/tests/03-KnowledgeAndAction/ToolUse.Tests
dotnet test dotnet/tests/03-KnowledgeAndAction/MCP.Tests
dotnet test dotnet/tests/03-KnowledgeAndAction/RAG.Tests
```
