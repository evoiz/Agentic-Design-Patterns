# Hosted Learning and Adaptation

Exposes the [Chapter 09 prompt adaptation workflow](../LearningAdaptation/README.md) through the Foundry Responses protocol (`LearningAdaptationAgent` derives from the shared `StatelessWorkflowAgent`). Build, deploy, inference RBAC, and cleanup steps are in the [hosting guide](../../HOSTING.md).

## Request contract

- **Input:** the last user message is the starting prompt (up to 4,000 characters).
- **Processing:** one bounded iteration — 3 baseline cases, 1 curator call, up to 3 candidate cases.
- **Output:** baseline and best scores, whether the prompt improved, and the best instructions for review.
- **State:** none. Each request is isolated; nothing is promoted, stored, or deployed. Streaming returns the buffered final result.

## Run and invoke

```powershell
dotnet run --project .\dotnet\src\LearningAdaptation.Hosted
azd ai agent invoke chapter09-learning-adaptation --local --protocol responses 'You are a helpful support assistant. Answer customer questions briefly.'
azd deploy chapter09-learning-adaptation --no-prompt
azd ai agent invoke chapter09-learning-adaptation --protocol responses 'You are a helpful support assistant. Answer customer questions briefly.'
```

Sensitive prompt/response capture is disabled in this demo's agent spans.
