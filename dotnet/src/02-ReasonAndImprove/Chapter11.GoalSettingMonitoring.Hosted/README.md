# Chapter 11 — Hosted Goal Setting and Monitoring

Exposes the [Chapter 11 goal-setting workflow](../Chapter11.GoalSettingMonitoring/README.md) through the Foundry Responses protocol (`GoalSettingAgent` derives from the shared `StatelessWorkflowAgent`). Build, deploy, inference RBAC, and cleanup steps are in the [hosting guide](../../../HOSTING.md).

## Request contract

Send a plain use case (sample goals, five-iteration budget) or JSON:

```json
{
  "useCase": "Write Python code to parse a CSV file and report row counts.",
  "goals": ["Use the standard library", "Handle an empty file", "Print the row count"],
  "maxIterations": 3
}
```

- `useCase` ≤ 2,000 chars; 1–10 goals of ≤ 300 chars; `maxIterations` 1–5. Invalid input fails before any model call.
- The response contains the goal status, iterations used, and final code. The host never saves or executes code.
- `RequestIsolationKeyProvider` gives every request a fresh session partition (keyed by response ID) even when service callers omit the platform user header; no caller history is reused.
- Streaming returns the buffered final result; cancellation flows to every model call.

## Run and invoke

```powershell
dotnet run --project .\dotnet\src\02-ReasonAndImprove\Chapter11.GoalSettingMonitoring.Hosted
azd ai agent invoke chapter11-goal-setting-monitoring --local --protocol responses "Write Python code to find the longest binary gap of a positive integer."
azd deploy chapter11-goal-setting-monitoring --no-prompt
azd ai agent invoke chapter11-goal-setting-monitoring --protocol responses "Write Python code to find the longest binary gap of a positive integer."
```

## Telemetry, cost, and tradeoffs

Generation, review, and judge spans capture sensitive data: use cases, goals, code, and feedback may appear in traces. Each iteration makes three model calls (up to five iterations). The model reviewer and judge can make correlated mistakes; use tests, linters, and human review before adopting generated code.
