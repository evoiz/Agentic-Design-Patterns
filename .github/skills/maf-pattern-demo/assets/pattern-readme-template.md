# <Pattern Name>

> <Faithful port / adaptation / paper-inspired teaching demo / original demo>, implemented with Microsoft Agent Framework (C#) on Microsoft Foundry.

Template instructions: fill the core sections, remove placeholders, and omit optional
variants/hosting sections that do not apply. Do not invent notebook or book metadata.

## Source and Scope

- Source: <author/title, local path or primary URL, relevant method/section/example;
  include commit/tag or paper version when available>.
- Fidelity: <what is preserved; what is independently implemented or changed, and why>.
- Implemented mechanism: <specific observable behavior>.
- Scope: <assumptions, synthetic data/service substitutions and unimplemented components>.
- Attribution/terms: <applicable source license/notices when reusing material; do not infer a grant>.

For a paper, distinguish its reported results from this teaching demo. State whether
training, datasets, model assumptions or evaluation procedures were changed/omitted.

## The Pattern in One Paragraph
<Problem it solves, core idea, 2–4 sentences.>

Link an independently drawn canonical `.drawio` and matching SVG under `docs\images`.
Show exclusive branches, return observations, joins and bounded/incomplete paths.
State whether the graph is conceptual or the exact executable workflow.

## What This Demo Shows
| Variant | Source idea | MAF / Foundry building blocks | What to observe |
|---|---|---|---|
| <name> (default MAF example) | <notebook/example/method or original contract> | `...` | ... |
| <name> (Foundry-native, optional) | same | Distinct Foundry tool / workflow / evaluation capability | ... — or "not applicable because ..."; do not count merely hosting the existing MAF code. |
| Hosted companion (when suitable) | same | Existing MAF workflow + Foundry protocol host | Link `../<PatternName>.Hosted/README.md`, or state the concrete hosting limitation. Hosting does not imply a separate Foundry-native pattern. |

## Setup
1. Prerequisites: .NET 10 SDK, a Foundry project with a model deployment and its API key.
2. Configure shared user-secrets once — link `../../README.md` from the new pattern README.
3. Foundry-native variant only: <additional configuration, credentials, cost/resource side effects>.
4. Run from the repository root:
   ```powershell
   dotnet run --project .\dotnet\src\<PatternName>          # small default
   dotnet run --project .\dotnet\src\<PatternName> -- A     # single variant
   ```

State the actual default, supported options and costful/opt-in variants. Link the shared
live-test instructions rather than repeating environment-secret setup.

## Behavior and Acceptance

Describe input/output shapes with a small illustrative example. Identify typed
handoffs, state lifetime, tool permissions and the immutable criteria checked by code.
Separate model-judged quality from deterministic acceptance. State loop/call/deadline
bounds, cancellation/error behavior and what happens on uncertainty or exhaustion.
Do not present code-shape validation as proof of semantic correctness.

## Where to Look in Code
| File / method | Pattern responsibility |
|---|---|
| [<core method>](<relative-file-path>) | <the small block that implements the pattern> |
| [<variant or contract>](<relative-file-path>) | <what differs and why> |

Use stable file/method pointers, not line numbers that drift during refactoring.

## Source to C# Mapping
| Source mechanism / pseudocode / original contract | C# / MAF implementation | Adaptation and evidence |
|---|---|---|
| ... | ... | <preserved/changed/simulated; focused test> |

For a faithful notebook port, map each in-scope idea. For external Python, map state
and control semantics rather than line-by-line syntax. For a paper, cite the relevant
section/algorithm and distinguish implemented components from deferred ones.

## Deterministic Checks

```powershell
dotnet test .\dotnet\tests\<PatternName>.Tests
```

<What these checks establish and what requires live models or independent evaluation.
Do not copy the paper's headline scores or claim benchmark parity.>

## When to Use
- ...

## When NOT to Use
- ...

## Real-World Scenarios
1. **<Domain>** — <concrete flow, why this pattern fits>.
2. ...
3. ...

## Extending with Microsoft Foundry (optional)
- <Tracing / evaluations / Agent Service / Foundry Local / Content Safety — what and why>

## Hosted Deployment Companion (optional)
- Suitability: <supported protocol, dependencies, state/interaction model, or concrete reason for skipping>.
- Project and deployment guide: <link to separate hosted project; preserve the console demo>.
- Request contract: <accepted inputs, final output, conversation behavior, and streaming behavior>.
- Common deployment/authentication/RBAC/costs: link `../../HOSTING.md` from the new pattern README.

Keep detailed protocol contracts and service-specific commands in the companion README;
do not repeat the common operational guide in every pattern.

Keep READMEs evergreen and suitable for public sharing. Never include verification reports, deployment status, deployed versions/dates, environment-specific identifiers, invocation results, or evaluation run history. Describe implementation scope and behavior in the relevant sections, not as limitations. Keep resource-removal guidance separate from pattern tradeoffs and scoped to resources the reader owns.

## Tradeoffs
- <Mechanism tradeoffs supported by source/reasoning: latency, error propagation,
  cost, model uncertainty. Keep scope/deviations above; don't import unmeasured paper
  performance claims or include deployment status/cleanup as architectural tradeoffs.>
