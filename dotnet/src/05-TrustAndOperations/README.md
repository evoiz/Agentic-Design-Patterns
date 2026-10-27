# 05 · Trust & Operations

> **Guiding question:** How does the system stay safe, reliable, and affordable in production?

These patterns make agentic systems **production systems**: they bound behaviour, recover from failure, control cost, measure quality, and package the runtime that hosts the agent.

Part of the five-family taxonomy described in [Agentic design patterns and families](../../../docs/design-patterns.md#5-trust--operations). For setup, user-secrets, and hosted deployment, see the [.NET guide](../../README.md).

## Patterns

| Chapter | Pattern | Use when | Demo | Hosted companion | Tests |
|---|---|---|---|---|---|
| 12 | Exception Handling and Recovery | Tools, models, or dependencies can fail and the agent must retry, fall back, or escalate. | [Chapter12.ExceptionHandling](Chapter12.ExceptionHandling/README.md) | [Chapter12.ExceptionHandling.Hosted](Chapter12.ExceptionHandling.Hosted/README.md) | [`ExceptionHandling.Tests`](../../tests/05-TrustAndOperations/ExceptionHandling.Tests) |
| 16 | Resource Optimization | Cost and latency matter; requests can be routed to cheaper or faster models. | [Chapter16.ResourceOptimization](Chapter16.ResourceOptimization/README.md) | [Chapter16.ResourceOptimization.Hosted](Chapter16.ResourceOptimization.Hosted/README.md) | [`ResourceOptimization.Tests`](../../tests/05-TrustAndOperations/ResourceOptimization.Tests) |
| 18 | Guardrails | Inputs, outputs, or actions must be screened against safety and policy rules. | [Chapter18.Guardrails](Chapter18.Guardrails/README.md) | [Chapter18.Guardrails.Hosted](Chapter18.Guardrails.Hosted/README.md) | [`Guardrails.Tests`](../../tests/05-TrustAndOperations/Guardrails.Tests) |
| 19 | Evaluation & Monitoring | Quality must be measured, compared, and tracked over time. | [Chapter19.Evaluation](Chapter19.Evaluation/README.md) | [Chapter19.Evaluation.Hosted](Chapter19.Evaluation.Hosted/README.md) | [`Evaluation.Tests`](../../tests/05-TrustAndOperations/Evaluation.Tests) |
| E01 | Agent Harness | A packaged runtime should provide todos, bounded tools, compaction, tracing, and completion checks. | [Extension01.AgentHarness](Extension01.AgentHarness/README.md) | [Extension01.AgentHarness.Hosted](Extension01.AgentHarness.Hosted/README.md) | [`AgentHarness.Tests`](../../tests/05-TrustAndOperations/AgentHarness.Tests) |

## Design guidance

This family is cross-cutting: it applies to every other family. Following **control before autonomy**, add tracing, limits, and evaluation before increasing autonomy.

## Run the tests for this family

```powershell
dotnet test dotnet/tests/05-TrustAndOperations/ExceptionHandling.Tests
dotnet test dotnet/tests/05-TrustAndOperations/ResourceOptimization.Tests
dotnet test dotnet/tests/05-TrustAndOperations/Guardrails.Tests
dotnet test dotnet/tests/05-TrustAndOperations/Evaluation.Tests
dotnet test dotnet/tests/05-TrustAndOperations/AgentHarness.Tests
```
