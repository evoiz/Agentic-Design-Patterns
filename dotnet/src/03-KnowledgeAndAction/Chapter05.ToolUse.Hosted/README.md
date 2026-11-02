# Chapter 05 — Hosted Tool Use

The [function-tool pattern](../Chapter05.ToolUse/README.md) behind the Foundry **Responses protocol**. The host exposes local typed functions, not the console's separate native web-search, File Search, or Code Interpreter modes.

## Request contract

| Latest user input | Available tools |
|---|---|
| `search: <query>` | Sample information lookup |
| `calculate: <arithmetic request>` | Typed decimal calculator |
| `stock: <ticker>` | Simulated AAPL/GOOGL/MSFT lookup |
| Unprefixed text | All three tools; the model selects |

Prefixes are case-insensitive. Empty inputs fail. Arithmetic text remains accepted: `(5 + 7) * 3` becomes two typed calls (`Add`, then `Multiply`); `10!` uses `Factorial`. The calculator supports add/subtract/multiply/divide and integer factorial 0–27, with explicit arithmetic errors. It never evaluates arbitrary code or parses an expression in application code.

Search and prices are static samples, never live information. Each request creates a fresh agent without persisted conversation history. Streaming returns one buffered completed answer; cancellation/provider failures propagate. Tool failures are returned through MAF's function-call error mechanism, not invented successful tool data.

## Where to look

- [`ToolUseHostedAgent`](ToolUseHostedAgent.cs): shared stateless adapter.
- [`ToolUseAgent.Create`](../Chapter05.ToolUse/ToolUseAgent.cs): tool registration and selected capabilities.
- [`ToolUseFunctions`](../Chapter05.ToolUse/ToolUseFunctions.cs): typed arithmetic and sample lookups.

## Run and deploy

See [common hosting guidance](../../../HOSTING.md) for settings, managed identity, inference RBAC, packaging, sensitive tracing, and agent-scoped cleanup. From the repository root:

```powershell
dotnet run --project .\dotnet\src\03-KnowledgeAndAction\Chapter05.ToolUse.Hosted
dotnet test .\dotnet\tests\03-KnowledgeAndAction\ToolUse.Tests
```

Optional host argument: `-- --deployment <alias>`. From `dotnet`:

```powershell
azd ai agent invoke chapter05-tool-use --local --protocol responses 'search: capital of France'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'calculate: (5 + 7) * 3'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'stock: AAPL'
azd deploy chapter05-tool-use --no-prompt
azd ai agent show chapter05-tool-use --output json
```

Verify current runtime inference access before remote calls; omit `--local` for the deployed service. Keep the local listener private and stop it with Ctrl+C. `Chapter05.ToolUse` tracing can include sensitive tool arguments/results.

## Tradeoffs

Narrow typed contracts are easier to audit but compound math needs extra tool round trips. Treat arguments and retrieved content as untrusted. External replacements need freshness, timeouts, attribution, and deterministic authorization before any writes.
