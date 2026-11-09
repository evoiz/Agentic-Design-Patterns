# Hosted Goal Setting and Monitoring

This companion exposes the [goal-setting workflows](../GoalSettingMonitoring/README.md) through Foundry Responses. Requests are stateless: each call processes its input without reusing prior caller history.

## Request contract

Send plain text to create a .NET implementation brief using sample goals, or provide JSON:

```json
{
  "useCase": "Create a .NET utility that reads a text file and reports its line count.",
  "goals": ["Handle an empty file", "Include a verification approach"],
  "maxIterations": 3,
  "scenario": "DotNetDeliverable"
}
```

The `scenario` is optional and accepts `DotNetDeliverable` (default) or `PythonCode`. `useCase` is limited to 2,000 characters, there must be 1–10 goals of at most 300 characters, and `maxIterations` is 1–5. Invalid input fails before a model call. The .NET output is accepted only when the exact model judge says `True` and deterministic code checks C#, .NET 10, non-empty title/summary, and at least three non-empty steps. Exhaustion returns an incomplete result with criteria feedback. The Python scenario remains available as an explicit compatibility option.

The response contains status, iterations, and the final artifact. Neither scenario saves nor executes generated code in the host. `RequestIsolationKeyProvider` assigns a fresh response-scoped partition even if the caller omits a platform user header. Streaming buffers one completed workflow result; cancellation and errors propagate.

## Run and invoke

Configure shared settings as described in [dotnet/README.md](../../README.md). In one terminal:

```powershell
dotnet run --project .\dotnet\src\GoalSettingMonitoring.Hosted
```

In another terminal:

```powershell
azd ai agent invoke chapter11-goal-setting-monitoring --local --protocol responses "Plan a small .NET line-counting utility."
azd ai agent invoke chapter11-goal-setting-monitoring --local --protocol responses '{"useCase":"Write a Python utility","goals":["Return a function"],"maxIterations":2,"scenario":"PythonCode"}'
```

Deploy and operate the reader-owned service using the [hosting guide](../../HOSTING.md), including managed identity, model inference RBAC, packaging, cost, and scoped cleanup:

```powershell
azd deploy chapter11-goal-setting-monitoring --no-prompt
azd ai agent invoke chapter11-goal-setting-monitoring --protocol responses "Plan a small .NET line-counting utility."
```

Sensitive prompt/response telemetry is disabled by default. To opt in for a controlled debugging environment, set `Foundry__Telemetry__IncludeSensitiveData=true` for the hosted process. Traces can contain requests, generated artifacts, reviews, and feedback.

## Cost and behavior

Each round makes three model calls, up to five rounds. A model reviewer and judge can make correlated mistakes; deterministic brief checks enforce only the documented hard criteria, not semantic correctness. Review or test artifacts before adopting them.
