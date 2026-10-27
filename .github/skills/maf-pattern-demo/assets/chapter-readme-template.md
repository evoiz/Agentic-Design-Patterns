# Chapter <NN> — <Pattern Name>

> Book: *Agentic Design Patterns* (Antonio Gulli), Chapter <NN>. Implemented with Microsoft Agent Framework (C#) on Microsoft Foundry.

## The Pattern in One Paragraph
<Problem it solves, core idea, 2–4 sentences.>

```mermaid
flowchart LR
    Input --> StepA --> StepB --> Output
```

## What This Demo Shows
| Variant | Source notebook | MAF / Foundry building blocks | What to observe |
|---|---|---|---|
| <name> (MAF) | `Chapter_<NN>_...ipynb` | `...` | ... |
| <name> (Foundry-native, optional) | same | Distinct Foundry tool / workflow / evaluation capability | ... — or "not applicable because ..."; do not count merely hosting the existing MAF code. |
| Hosted companion (when suitable) | same | Existing MAF workflow + Foundry protocol host | Link `../Chapter<NN>.<PatternName>.Hosted/README.md`, or state the concrete hosting limitation. Hosting does not imply a separate Foundry-native pattern. |

## Setup
1. Prerequisites: .NET 10 SDK, a Foundry project with a model deployment and its API key.
2. For local runs, configure shared user-secrets once (shared by all chapters) — see [dotnet/README.md](../../../README.md).
3. For cloud-agent validation, document the GitHub Actions path: the repo's `live-foundry-tests.yml` workflow uses the protected `foundry-integration` environment and `Foundry__*` environment variables sourced from GitHub environment secrets.
4. Foundry-native variant only: <extra steps, e.g. `az login`, deploy hosted agent>.
5. Run locally:
   ```powershell
   dotnet run --project dotnet/src/<Family>/Chapter<NN>.<PatternName>          # all variants
   dotnet run --project dotnet/src/<Family>/Chapter<NN>.<PatternName> -- A     # single variant
   ```
6. Validate in GitHub Actions when local secrets are unavailable:
   - Open **Actions** → **Live Foundry smoke tests**
   - Run the workflow manually after the workflow file exists on the default branch
   - Optionally pass a deployment alias such as `fast` or `quality`

## Notebook → C# Mapping
| Python (LangChain/ADK/...) | C# (MAF) |
|---|---|
| ... | ... |

## When to Use
- ...

## When NOT to Use
- ...

## Real-World Scenarios
1. **<Domain>** — <concrete flow, why this pattern fits>.
2. ...
3. ...

## Extending with Microsoft Foundry
- <Tracing / evaluations / Agent Service / Foundry Local / Content Safety — what and why>

## Hosted Deployment Companion
- Suitability: <supported protocol, dependencies, state/interaction model, or concrete reason for skipping>.
- Project and deployment guide: <link to separate hosted project; preserve the console demo>.
- Authentication: <local developer credentials and Azure managed identity; deployment permissions are separate from runtime inference access. Before remote invocation, verify/assign Cognitive Services OpenAI User to the deployed instance_identity.principal_id at the model-hosting Foundry account scope with RBAC-write approval; allow propagation and recheck after redeployment>.
- Packaging: <remote-build context including references, or bundled published dependencies; exclude secrets>.
- Request contract: <accepted inputs, final output, conversation behavior, and streaming behavior>.
- Deployment: <link to reusable setup/deploy/invoke instructions using the reader's own project and model; document required permissions and costs, not deployment history>.

Keep READMEs evergreen and suitable for public sharing. Never include verification reports, deployment status, deployed versions/dates, environment-specific identifiers, invocation results, or evaluation run history. Describe implementation scope and behavior in the relevant sections, not as limitations. Keep resource-removal guidance separate from pattern tradeoffs and scoped to resources the reader owns.

## Risks & Limitations
- <Design-pattern tradeoffs from ai-research: e.g. latency, error propagation, cost, model uncertainty. Do not list implementation-specific omissions, package maturity, deployment state, or cleanup tasks.>
