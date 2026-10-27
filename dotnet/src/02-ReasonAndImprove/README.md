# 02 · Reason & Improve

> **Guiding question:** How does the agent think about and improve its work?

These patterns raise the **quality of output** by spending extra inference: structured reasoning, critique-and-revise loops, explicit goals with progress checks, adaptation from feedback, and open-ended exploration.

Part of the five-family taxonomy described in [Agentic design patterns and families](../../../docs/design-patterns.md#2-reason--improve). For setup, user-secrets, and hosted deployment, see the [.NET guide](../../README.md).

## Patterns

| Chapter | Pattern | Use when | Demo | Hosted companion | Tests |
|---|---|---|---|---|---|
| 04 | Reflection | Output quality matters more than latency, and a critic can judge it against clear criteria. | [Chapter04.Reflection](Chapter04.Reflection/README.md) | [Chapter04.Reflection.Hosted](Chapter04.Reflection.Hosted/README.md) | [`Reflection.Tests`](../../tests/02-ReasonAndImprove/Reflection.Tests) |
| 09 | Learning & Adaptation | Behaviour should improve from feedback or evaluation results over time. | [Chapter09.LearningAdaptation](Chapter09.LearningAdaptation/README.md) | [Chapter09.LearningAdaptation.Hosted](Chapter09.LearningAdaptation.Hosted/README.md) | [`LearningAdaptation.Tests`](../../tests/02-ReasonAndImprove/LearningAdaptation.Tests) |
| 11 | Goal Setting & Monitoring | Work must converge on explicit, measurable goals within an iteration budget. | [Chapter11.GoalSettingMonitoring](Chapter11.GoalSettingMonitoring/README.md) | [Chapter11.GoalSettingMonitoring.Hosted](Chapter11.GoalSettingMonitoring.Hosted/README.md) | [`GoalSettingMonitoring.Tests`](../../tests/02-ReasonAndImprove/GoalSettingMonitoring.Tests) |
| 17 | Reasoning | Multi-step problems benefit from chain-of-thought, self-correction, or ReAct-style reasoning. | [Chapter17.Reasoning](Chapter17.Reasoning/README.md) | [Chapter17.Reasoning.Hosted](Chapter17.Reasoning.Hosted/README.md) | [`Reasoning.Tests`](../../tests/02-ReasonAndImprove/Reasoning.Tests) |
| 21 | Exploration & Discovery | The solution space is open-ended and benefits from generating and reviewing hypotheses. | [Chapter21.ExplorationDiscovery](Chapter21.ExplorationDiscovery/README.md) | [Chapter21.ExplorationDiscovery.Hosted](Chapter21.ExplorationDiscovery.Hosted/README.md) | [`Chapter21.Tests`](../../tests/02-ReasonAndImprove/Chapter21.Tests) |

## Design guidance

Every loop in this family needs a **stop condition** (iteration cap, goal check, or quality threshold) and is best paired with Evaluation from Trust & Operations.

## Run the tests for this family

```powershell
dotnet test dotnet/tests/02-ReasonAndImprove/Reflection.Tests
dotnet test dotnet/tests/02-ReasonAndImprove/LearningAdaptation.Tests
dotnet test dotnet/tests/02-ReasonAndImprove/GoalSettingMonitoring.Tests
dotnet test dotnet/tests/02-ReasonAndImprove/Reasoning.Tests
dotnet test dotnet/tests/02-ReasonAndImprove/Chapter21.Tests
```
