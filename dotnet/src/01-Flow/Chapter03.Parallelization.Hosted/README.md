# Chapter 03 — Hosted Parallelization

The [parallelization pattern](../Chapter03.Parallelization/README.md) behind the Foundry **Responses protocol**.

## Request contract

| Latest user input | Three independent workers |
|---|---|
| `topic: <text>` or unprefixed text | Summary, related questions, key terms |
| `research: <text>` | Renewable energy, electric vehicles, carbon capture |

Prefixes are case-insensitive; empty input is rejected. Every worker receives the caller's text; a fourth call synthesizes all results. Research uses model knowledge, not live search.

The agent starts a fresh workflow for every request and returns only its completed synthesis. A worker failure fails the request, not a partial answer. Streaming emits one buffered result; cancellation propagates. No agent conversation history is persisted.

## Where to look

- [`ParallelizationAgent.RunAsync`](ParallelizationAgent.cs): request parsing and thin shared stateless adapter.
- [`ParallelizationWorkflow.RunAsync`](../Chapter03.Parallelization/ParallelizationWorkflow.cs): concurrent MAF workflow and synthesis.
- [`Program`](Program.cs): shared model configuration and protocol registration.

## Run and deploy

Follow [common hosting guidance](../../../HOSTING.md) for configuration, managed identity, bundled dependencies, inference RBAC, telemetry, and agent-scoped cleanup.

From the repository root:

```powershell
dotnet run --project .\dotnet\src\01-Flow\Chapter03.Parallelization.Hosted
dotnet test .\dotnet\tests\01-Flow\Parallelization.Tests
```

Optional host argument: `-- --deployment <alias>`. From `dotnet` while the private local host runs:

```powershell
azd ai agent invoke chapter03-parallelization --local --protocol responses 'topic: How can cities reduce emissions?'
azd ai agent invoke chapter03-parallelization --local --protocol responses 'research: Compare sustainable technology options.'
azd deploy chapter03-parallelization --no-prompt
azd ai agent show chapter03-parallelization --output json
```

Verify current runtime inference access before remote calls; omit `--local` for the deployed service. Stop the local host with Ctrl+C. Sensitive-content tracing uses `Chapter03.Parallelization`.

## Tradeoffs

Four model calls increase cost. The slowest worker and endpoint throttling determine latency. Keep attribution and decide any retry/partial-output policy explicitly before replacing the current fail-whole-request behavior.
