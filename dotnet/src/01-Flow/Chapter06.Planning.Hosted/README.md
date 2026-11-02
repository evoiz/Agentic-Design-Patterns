# Chapter 06 — Hosted Planning

The [plan-and-write pattern](../Chapter06.Planning/README.md) behind the Foundry **Responses protocol**.

## Request contract

The latest nonempty user message is the topic. A planner returns three to six nonempty steps; a writer receives the original topic and that plan. The final response contains both `### Plan` and `### Summary`.

Each request uses fresh agents with no persisted conversation history. Streaming emits one buffered result after both calls. Invalid plans fail before writer invocation; provider failures, empty summaries, and cancellation fail explicitly.

Deep Research remains a separate console mode. Exposing its long-running background operation would require durable job/polling and timeout semantics rather than silently substituting it for this two-call contract.

## Where to look

- [`PlanningAgent.RunAsync`](PlanningAgent.cs): shared stateless adapter and formatted result.
- [`PlanningWorkflow.RunAsync`](../Chapter06.Planning/PlanningWorkflow.cs): two-executor MAF graph and plan validation.
- [`PlanningAgents.Create`](../Chapter06.Planning/PlanningAgents.cs): planner/writer instructions.

## Run and deploy

See [common hosting guidance](../../../HOSTING.md) for configuration, managed identity, inference RBAC, packaging, telemetry, and agent-scoped cleanup. From the repository root:

```powershell
dotnet run --project .\dotnet\src\01-Flow\Chapter06.Planning.Hosted
dotnet test .\dotnet\tests\01-Flow\Planning.Tests
```

Optional host argument: `-- --deployment <alias>`. From `dotnet`:

```powershell
azd ai agent invoke chapter06-planning --local --protocol responses 'Reinforcement learning in robotics'
azd deploy chapter06-planning --no-prompt
azd ai agent show chapter06-planning --output json
```

Verify the current runtime identity's inference access before remote calls; omit `--local` for the deployed service. Keep the local listener private and stop it with Ctrl+C. Sensitive tracing under `Chapter06.Planning` can export topics, plans, and summaries.

## Tradeoffs

A plan can be structurally valid but wrong, incomplete, or unnecessary. Validate business requirements separately and never treat a generated plan as permission to act. Planning adds latency and model usage before writing.
