# Chapter 20 — Hosted Prioritization

Expose the [priority-proposal / deterministic-scheduler pattern](../Chapter20.Prioritization/README.md) through Foundry Responses.

## Where to look in code

[PrioritizationAgent.cs](PrioritizationAgent.cs), `ParseTaskDescriptions`: both input contracts; `RunAsync`: presentation over the shared stateless adapter. [PrioritizationWorkflow.ProposeTaskAsync](../Chapter20.Prioritization/PrioritizationWorkflow.cs) validates/retries; [PriorityScheduler.Schedule](../Chapter20.Prioritization/PriorityScheduler.cs) owns stable queue order.

## Request contract

Responses `input` accepts:

- Plain text: each nonempty newline-separated line is one task.
- JSON text: `{"tasks":["Urgent: restore sign-in","Review release notes"]}`.

Batches contain **1–20** nonempty descriptions. Malformed JSON or absent/null `tasks` fails explicitly. Request-local IDs restart at `TASK-001`. Prior turns and task state are not reused.

Each task receives an enum-constrained P0/P1/P2 proposal and a validated worker (Worker A, Worker B, Review Team); an omitted worker defaults to Worker A. One invalid proposal may be corrected once, so there are at most **two model calls per task**. Exhaustion is an error, never a partial success.

After all proposals, code orders P0 → P1 → P2 and preserves arrival order for ties. The response includes IDs, priorities, workers, and descriptions. Streaming buffers the complete queue. Cancellation propagates; no external worker assignment or persistent task mutation occurs.

## Local commands

Configure [shared settings](../../../README.md), then:

```powershell
dotnet run --project dotnet\src\01-Flow\Chapter20.Prioritization.Hosted
```

In another terminal, from this project's directory:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter20-prioritization --local --protocol responses '{"tasks":["Urgent: restore sign-in","Review release notes"]}'
```

## Deploy and operate

Follow [HOSTING.md](../../../HOSTING.md) for reader-owned project/model selection, managed identity/inference RBAC, bundled dependencies, sensitive tracing, cost, and scoped cleanup. Local model access supports developer credentials or user-secrets; do not provide API keys in Azure. After setup, from this project's directory:

```powershell
azd deploy chapter20-prioritization --no-prompt
azd ai agent show chapter20-prioritization --output json
# Verify runtime inference access before remote invocation.
azd ai agent invoke chapter20-prioritization --protocol responses "Urgent: restore sign-in"
```

Urgency/assignment remain model judgments. The deterministic scheduler does not supply authoritative SLA policy, capacity reservations, durable cross-request state, or approval. Add those boundaries before connecting consequential actions.
