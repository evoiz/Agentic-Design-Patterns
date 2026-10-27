# 04 · Collaboration

> **Guiding question:** Who participates in the work?

Collaboration patterns distribute work across **multiple actors**: specialised agents, remote agents over a protocol, and humans who approve, correct, or take over.

Part of the five-family taxonomy described in [Agentic design patterns and families](../../../docs/design-patterns.md#4-collaboration). For setup, user-secrets, and hosted deployment, see the [.NET guide](../../README.md).

## Patterns

| Chapter | Pattern | Use when | Demo | Hosted companion | Tests |
|---|---|---|---|---|---|
| 07 | Multi-Agent Collaboration | A problem decomposes into roles with distinct instructions, tools, or models. Includes `Chapter07.MultiAgent.Distributed`. | [Chapter07.MultiAgent](Chapter07.MultiAgent/README.md) | [Chapter07.MultiAgent.Hosted](Chapter07.MultiAgent.Hosted/README.md) | [`MultiAgent.Tests`](../../tests/04-Collaboration/MultiAgent.Tests) |
| 13 | Human-in-the-Loop | Decisions are high-stakes, ambiguous, or require accountable approval. | [Chapter13.HumanInTheLoop](Chapter13.HumanInTheLoop/README.md) | [Chapter13.HumanInTheLoop.Hosted](Chapter13.HumanInTheLoop.Hosted/README.md) | [`HumanInTheLoop.Tests`](../../tests/04-Collaboration/HumanInTheLoop.Tests) |
| 15 | Inter-Agent Communication (A2A) | Agents built or hosted independently must discover and call each other. | [Chapter15.A2A](Chapter15.A2A/README.md) | [Chapter15.A2A.Hosted](Chapter15.A2A.Hosted/README.md) | — |

## Design guidance

Multi-agent architecture is an **optimisation for decomposition**, not a default. Add agents only when a single agent with good tools and a workflow is demonstrably insufficient.

## Run the tests for this family

```powershell
dotnet test dotnet/tests/04-Collaboration/MultiAgent.Tests
dotnet test dotnet/tests/04-Collaboration/HumanInTheLoop.Tests
```
