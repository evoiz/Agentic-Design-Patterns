# Agentic Design Patterns with C# and Microsoft Agent Framework

This is my **C# / Microsoft Agent Framework (MAF)** focused fork: runnable examples, explicit workflow contracts and editable diagrams for learning how agentic patterns work. It builds on Antonio Gulli's *Agentic Design Patterns* and the work in [evoiz/Agentic-Design-Patterns](https://github.com/evoiz/Agentic-Design-Patterns). The goal here is a useful starting point that continues to evolve with additional, testable MAF patterns.

Start locally with one Foundry chat deployment. Hosting, managed tools and additional Azure services are optional—not prerequisites for understanding the patterns.

## Get started

You need Git, the **.NET 10 SDK**, NuGet access, and an accessible **Microsoft Foundry resource/project with one deployed chat model**. The deployment must support structured JSON output for the first example; tool-using patterns also need tool calling. You need that model-hosting resource's API key. No hosted agent, container registry, embedding model, Azure AI Search, Content Safety, Node or Python is needed for this first run.

Run these PowerShell commands:

```powershell
git clone https://github.com/cedricmendelin/Agentic-Design-Patterns-MAF.git
Set-Location Agentic-Design-Patterns-MAF

dotnet user-secrets set "Foundry:ProjectEndpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project .\dotnet\src\Shared
dotnet user-secrets set "Foundry:ApiKey" "<resource-api-key>" --project .\dotnet\src\Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<deployment-name>" --project .\dotnet\src\Shared

dotnet run --project .\dotnet\src\PromptChaining -- specs
```

Use the **deployment name**, not simply a model name. `ProjectEndpoint` is the standard Foundry project endpoint, not a chat completions URL. The current shared client derives `https://<resource>.openai.azure.com/openai/v1/` from it; the API key must authorize inference on that resource. See the [C# setup guide](dotnet/README.md) for environment overrides, model aliases and optional-service requirements.

The example prints extracted requirements, then formatted specifications. Two model stages pass a structured handoff; deterministic C# validation checks the final JSON. Generated wording varies. Open [PromptChaining](dotnet/src/PromptChaining/README.md) to see the output contract and the small workflow implementing it.

Expect `SpecificationExtractor`, `SpecificationJsonFormatter` and `Final output` sections, with a final shape such as:

```json
{"cpu":"3.5 GHz octa-core processor","memory":"16GB of RAM","storage":"1TB NVMe SSD"}
```

All demos share `UserSecretsId = agentic-design-patterns-maf`. User-secrets live outside Git, but are **not an encrypted production vault**. Never commit keys, paste real keys into example commands, or print/list secrets. Model calls incur charges; start with one variant rather than `all`. Sensitive prompt/response telemetry is off by default.

Build the collection or run the first pattern's deterministic tests without cloud credentials:

```powershell
dotnet build .\dotnet\AgenticDesignPatterns.slnx
dotnet test .\dotnet\tests\PromptChaining.Tests
```

## Choose a pattern

A short learning path is **PromptChaining → Routing → ToolUse → Reflection → Memory / RAG**, then collaboration and operational controls as needed. [The design guide](docs/design-patterns.md) explains the concepts, tradeoffs, diagrams and the difference between conceptual flows and runnable demos.

Pattern names are the physical organization: `dotnet\src\<PatternName>` and `dotnet\tests\<PatternName>.Tests`. Families and book chapters are navigation metadata, not folder boundaries.

| Pattern | Family | Book chapter |
|---|---|---|
| [PromptChaining](dotnet/src/PromptChaining/README.md) | Flow | 1 |
| [Routing](dotnet/src/Routing/README.md) | Flow | 2 |
| [Parallelization](dotnet/src/Parallelization/README.md) | Flow | 3 |
| [Reflection](dotnet/src/Reflection/README.md) | Reason & Improve | 4 |
| [ToolUse](dotnet/src/ToolUse/README.md) | Knowledge & Action | 5 |
| [Planning](dotnet/src/Planning/README.md) | Flow | 6 |
| [MultiAgent](dotnet/src/MultiAgent/README.md) | Collaboration | 7 |
| [Memory](dotnet/src/Memory/README.md) | Knowledge & Action | 8 |
| [LearningAdaptation](dotnet/src/LearningAdaptation/README.md) | Reason & Improve | 9 |
| [MCP](dotnet/src/MCP/README.md) | Knowledge & Action | 10 |
| [GoalSettingMonitoring](dotnet/src/GoalSettingMonitoring/README.md) | Reason & Improve | 11 |
| [ExceptionHandling](dotnet/src/ExceptionHandling/README.md) | Trust & Operations | 12 |
| [HumanInTheLoop](dotnet/src/HumanInTheLoop/README.md) | Collaboration | 13 |
| [RAG](dotnet/src/RAG/README.md) | Knowledge & Action | 14 |
| [A2A](dotnet/src/A2A/README.md) | Collaboration | 15 |
| [ResourceOptimization](dotnet/src/ResourceOptimization/README.md) | Trust & Operations | 16 |
| [Reasoning](dotnet/src/Reasoning/README.md) | Reason & Improve | 17 |
| [Guardrails](dotnet/src/Guardrails/README.md) | Trust & Operations | 18 |
| [Evaluation](dotnet/src/Evaluation/README.md) | Trust & Operations | 19 |
| [Prioritization](dotnet/src/Prioritization/README.md) | Flow | 20 |
| [ExplorationDiscovery](dotnet/src/ExplorationDiscovery/README.md) | Reason & Improve | 21 |
| [AgentHarness](dotnet/src/AgentHarness/README.md) | Trust & Operations | Repository addition |
| [DecisionModels](dotnet/src/DecisionModels/README.md) | Flow | Repository addition |

## Beyond the first run

The [C# guide](dotnet/README.md) covers optional services and the protected live-model smoke tests. [HOSTING](dotnet/HOSTING.md) covers optional `.Hosted` companions, runtime identity, model-host inference permissions, packaging and costs. MCP has a `.Server` companion; MultiAgent also has `.Distributed`. Do not deploy every companion to learn a pattern.

[Update Conference demos](update-conference-26/README.md) compose selected patterns into a presentation; they are not the main onboarding path. [Research and the enterprise backlog](ai-research/agentic-patterns-backlog.md) describe possible future contracts such as verified context handoff, governed memory and controlled actions. Proposed patterns are not implemented capabilities.

## Resources and contributions

- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/)
- [Your first agent](https://learn.microsoft.com/agent-framework/get-started/your-first-agent), [workflows](https://learn.microsoft.com/agent-framework/get-started/workflows) and [C# samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples)
- [Context Management](https://agentic-design.ai/patterns/context-management)—a useful perspective on selecting, preserving and refreshing context beyond prompt wording
- [Contributing](CONTRIBUTING.md)—add a small example with a distinct contract, deterministic checks and an accurate diagram

## Attribution and reference material

Thanks to **Antonio Gulli** for the book and **evoiz** for the upstream repository this fork builds on. [`chapter_notebooks`](chapter_notebooks) and [`book`](book) remain historical/source references; they are not the setup path for this MAF-focused fork. Diagrams under [`docs\images`](docs/images) are independently drawn explanations with canonical editable sources.

No repository-wide license grant is asserted here. Original materials retain their authors' rights; consult their applicable terms before reuse. Adding a license requires confirmation of the rights to the relevant material.
