# Chapter 04 — Hosted Reflection

The [bounded reflection pattern](../Chapter04.Reflection/README.md) behind the Foundry **Responses protocol**.

## Request contract

| Latest user input | Behavior |
|---|---|
| `fact-check: <topic>` | Generate a paragraph and review accuracy/qualification |
| `product: <details>` or unprefixed text | Refine product copy against house style |
| `code-review: <Python task>` | Generate and review code; no code execution |

Prefixes are case-insensitive. Empty input fails. All modes share the same generator/reviewer graph, three-round default budget, and maximum of six agent invocations excluding provider retries. Every revision is reviewed.

The response contains the last reviewed draft, review, rounds used, and `Outcome: Accepted` or `Outcome: MaxRoundsReached`. Exhaustion is explicitly **not acceptance**. Empty drafts, malformed reviews, provider errors, and cancellation fail the request.

Requests have fresh agents and no persisted conversation history. Streaming emits one buffered final result. Product prompts deliberately encourage rich lifestyle copy while the reviewer enforces ≤40 words, an exact closing sentence, and supported claims.

## Where to look

- [`ReflectionAgent.RunAsync`](ReflectionAgent.cs): request modes and shared stateless adapter.
- [`ReflectionWorkflow.Create`](../Chapter04.Reflection/ReflectionWorkflow.cs): conditional retry/completion graph.
- [`ReflectionDemos.CreateAgents`](../Chapter04.Reflection/ReflectionDemos.cs): scenario prompts.

## Run and deploy

Follow [common hosting guidance](../../../HOSTING.md) for configuration, identity, inference RBAC, packaging, telemetry, and safe cleanup. From the repository root:

```powershell
dotnet run --project .\dotnet\src\02-ReasonAndImprove\Chapter04.Reflection.Hosted
dotnet test .\dotnet\tests\02-ReasonAndImprove\Reflection.Tests
```

Optional host argument: `-- --deployment <alias>`. From `dotnet`:

```powershell
azd ai agent invoke chapter04-reflection --local --protocol responses 'product: An insulated mug with app controls.'
azd ai agent invoke chapter04-reflection --local --protocol responses 'fact-check: Explain photosynthesis.'
azd ai agent invoke chapter04-reflection --local --protocol responses 'code-review: Write a factorial function with negative-input validation.'
azd deploy chapter04-reflection --no-prompt
azd ai agent show chapter04-reflection --output json
```

Verify runtime inference access before remote invocation; omit `--local` for the deployed service. Keep the local listener private and stop it with Ctrl+C. Sensitive tracing under `Chapter04.Reflection` can export drafts and critiques.

## Adapting evaluation examples

[eval.yaml](eval.yaml), the [sample dataset](datasets/chapter04-reflection-smoke/chapter04-reflection-smoke_dg.jsonl), and the [rubric](evaluators/chapter04-reflection-smoke/rubric_dimensions.json) are templates. Align outcomes, acceptance criteria, prefix behavior, and Python tasks with your own contract. Synthetic candidate responses are examples, not measured outputs; evaluation runs incur separate usage.

## Tradeoffs

The reviewer may reinforce the generator's errors. Independent evidence, deterministic tests, and human review remain necessary for consequential tasks. Bound rounds and distinguish acceptance from exhaustion.
