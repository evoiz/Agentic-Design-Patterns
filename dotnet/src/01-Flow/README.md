# 01 · Flow

> **Guiding question:** How does work move through the system?

Flow patterns decide the **shape of execution**: the order of steps, which branch to take, what runs concurrently, and what happens first. They are the most deterministic family and usually the first place to add structure around a single model call.

Part of the five-family taxonomy described in [Agentic design patterns and families](../../../docs/design-patterns.md#1-flow). For setup, user-secrets, and hosted deployment, see the [.NET guide](../../README.md).

## Patterns

| Chapter | Pattern | Use when | Demo | Hosted companion | Tests |
|---|---|---|---|---|---|
| 01 | Prompt Chaining | A task decomposes into fixed, sequential steps with checkable intermediate outputs. | [Chapter01.PromptChaining](Chapter01.PromptChaining/README.md) | — | [`PromptChaining.Tests`](../../tests/01-Flow/PromptChaining.Tests) |
| 02 | Routing | Inputs fall into distinct categories that need different handlers, models, or prompts. | [Chapter02.Routing](Chapter02.Routing/README.md) | [Chapter02.Routing.Hosted](Chapter02.Routing.Hosted/README.md) | [`Routing.Tests`](../../tests/01-Flow/Routing.Tests) |
| 03 | Parallelization | Independent sub-tasks can run concurrently and be aggregated. | [Chapter03.Parallelization](Chapter03.Parallelization/README.md) | [Chapter03.Parallelization.Hosted](Chapter03.Parallelization.Hosted/README.md) | [`Parallelization.Tests`](../../tests/01-Flow/Parallelization.Tests) |
| 06 | Planning | The steps are not known up front and must be generated, then executed. | [Chapter06.Planning](Chapter06.Planning/README.md) | [Chapter06.Planning.Hosted](Chapter06.Planning.Hosted/README.md) | [`Planning.Tests`](../../tests/01-Flow/Planning.Tests) |
| 20 | Prioritization | More work exists than capacity; tasks must be ranked by urgency, importance, or dependencies. | [Chapter20.Prioritization](Chapter20.Prioritization/README.md) | [Chapter20.Prioritization.Hosted](Chapter20.Prioritization.Hosted/README.md) | [`Prioritization.Tests`](../../tests/01-Flow/Prioritization.Tests) |
| Supplement | Decision Models | A branch, rank, or gate needs a typed, calibrated decision instead of free-form text. | [Extension02.DecisionModels](Extension02.DecisionModels/README.md) | [Extension02.DecisionModels.Hosted](Extension02.DecisionModels.Hosted/README.md) | [`Extension02.DecisionModels.Tests`](../../tests/01-Flow/Extension02.DecisionModels.Tests) |

## Design guidance

Start with a deterministic chain, add routing or parallel fan-out only when inputs or latency require it, and reach for planning last. Flow patterns frequently delegate the *content* of each step to the other families.

## Run the tests for this family

```powershell
dotnet test dotnet/tests/01-Flow/PromptChaining.Tests
dotnet test dotnet/tests/01-Flow/Routing.Tests
dotnet test dotnet/tests/01-Flow/Parallelization.Tests
dotnet test dotnet/tests/01-Flow/Planning.Tests
dotnet test dotnet/tests/01-Flow/Prioritization.Tests
dotnet test dotnet/tests/01-Flow/Extension02.DecisionModels.Tests
```
