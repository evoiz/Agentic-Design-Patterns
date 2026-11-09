# Contributing

This fork focuses on readable, runnable **C# / Microsoft Agent Framework** patterns. Keep the first example small; add autonomy only when its behavior is observable and useful.

## Add or improve a pattern

Use `dotnet\src\<PatternName>\<PatternName>.csproj` and `dotnet\tests\<PatternName>.Tests`. Families/book chapters belong in the catalog as metadata, not folder/project/namespace prefixes. Reuse `Shared`, `Shared.Hosting` and `TestSupport`; retain the shared secrets ID and pinned central package versions. A2A uses `A2A.Demo` as its assembly name to avoid colliding with the SDK's `A2A` package.

Give the README a short problem statement, when to use/not use it, the actual default and optional variants, expected output, and stable file/method pointers. Link shared [setup](dotnet/README.md) and [hosting](dotnet/HOSTING.md) rather than copying operational tutorials. Preserve original variants as secondary examples when changing a default.

Use deterministic code for immutable rules, authority, acceptance and termination. Keep uncertainty, incomplete work, cancellation and provider errors visible. Tests should cover the changed contract—not merely a successful model response—including budgets, failure propagation, output shape and relevant identity/approval boundaries. Generated code must not run on the host.

Assess hosting separately. Add a `.Hosted` companion only when its protocol/state/dependencies are suitable; explain any limitations. A host is an adapter, not a second pattern implementation. Don't enable prompt/response capture by default or commit keys, local state, results, environment identifiers or deployment status.

## Diagrams and navigation

Keep independently drawn `.drawio` sources and matching SVGs under `docs\images`. Label conceptual flows separately from executable demos. Review **direction, branch exclusivity, joins, tool-result returns and exhausted-budget paths**; valid XML does not prove a correct diagram.

With draw.io Desktop installed:

```powershell
.\scripts\Export-Diagrams.ps1 -DrawioPath 'C:\Program Files\draw.io\draw.io.exe'
.\scripts\Test-Diagrams.ps1
.\scripts\Test-Repository.ps1
```

SVGs contain embedded editable source and a newline-normalized source hash. The drift check does not need draw.io; visually review exports as well. Register new patterns in the [root catalog](README.md#choose-a-pattern), [C# family navigation](dotnet/README.md#pattern-families), design guide and `dotnet\AgenticDesignPatterns.slnx`. Update the [demo skill](.github/skills/maf-pattern-demo/SKILL.md) when conventions change.

## Validation and scope

Run the smallest affected deterministic suite first:

```powershell
dotnet test .\dotnet\tests\<PatternName>.Tests
```

For shared changes or moves, build/test both solutions using the [shared commands](dotnet/README.md#build-and-deterministic-tests). Live-model checks are separate, paid and credential-dependent. Preserve the protected manual workflow's trusted-default-branch and Environment safeguards. Do not infer permission to provision, deploy, modify RBAC or remove remote resources from a code change.

A new pattern should add a distinct testable contract, not duplicate an existing composition; the [backlog](ai-research/agentic-patterns-backlog.md) records proposals rather than promises. Credit source ideas and respect original-material rights. Do not assert or add a license without confirming the rights involved.
