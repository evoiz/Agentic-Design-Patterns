# Agentic design patterns and pattern families

This guide gives a common vocabulary for the demos in this repository. It summarises the basics of agentic systems and design patterns from Antonio Gulli's *Agentic Design Patterns* (see [`book/`](../book/)). It then introduces the **five pattern families** that the .NET solution is organised around.

- [What is an agentic system?](#what-is-an-agentic-system)
- [Levels of agent complexity](#levels-of-agent-complexity)
- [What is a design pattern?](#what-is-a-design-pattern)
- [Why cluster the patterns?](#why-cluster-the-patterns)
- [The five pattern families](#the-five-pattern-families)
- [Control before autonomy](#control-before-autonomy)
- [Composing patterns](#composing-patterns)
- [Mapping to Microsoft Agent Framework and Foundry](#mapping-to-microsoft-agent-framework-and-foundry)
- [Proposed contracts by family](#proposed-contracts-by-family)
- [Further reading](#further-reading)

## What is an agentic system?

An **agentic system** perceives its environment, decides what to do based on goals, and acts to reach those goals with some degree of autonomy. Traditional software follows a fixed script. An agent can interpret an ambiguous request, look things up, call other systems, ask clarifying questions, and adjust its next step based on what it observes.

The book describes recurring characteristics:

| Characteristic | Meaning |
|---|---|
| Autonomy | Acts without step-by-step human direction. |
| Goal orientation | Works towards an explicit objective rather than only answering a prompt. |
| Proactiveness and reactiveness | Takes initiative and responds to changes in its environment. |
| Tool use | Reaches beyond the model through APIs, databases, and services. |
| Memory | Retains information across steps and interactions. |
| Communication | Interacts with users, systems, and other agents. |

A practical way to picture an agent is a loop: **receive a goal → gather context → plan → act → observe and improve**. Each characteristic introduces engineering questions: how state is kept, when a tool is called, how agents coordinate, and how errors are recovered. Design patterns answer these questions.

## Levels of agent complexity

The book describes a spectrum of agent complexity. It helps to decide how much machinery a problem really needs.

| Level | Name | What it adds | Typical patterns |
|---|---|---|---|
| 0 | Core reasoning engine | An LLM answering only from its training data. No tools, memory, or environment access. | Prompting, reasoning |
| 1 | Connected problem-solver | External tools and retrieval, used across several steps. | Tool use, RAG, MCP |
| 2 | Strategic problem-solver | Planning, context engineering (selecting and packaging only the relevant information for each step), and self-improvement. | Planning, reflection, memory, goal monitoring, learning |
| 3 | Collaborative multi-agent system | A team of specialised agents coordinated like an organisation. | Multi-agent collaboration, A2A, human-in-the-loop |

Higher levels are not automatically better. Each level adds cost, latency, and failure modes. Start low and climb only when the problem requires it.

## What is a design pattern?

A design pattern is a **named, reusable solution to a recurring problem in a given context**. In classic software engineering, patterns such as Strategy or Observer give teams a shared language. Agentic design patterns do the same for systems built around language models. They are not rigid rules. They are proven templates that make agent logic easier to explain, review, test, and maintain.

The book presents each of its 21 patterns with the same anatomy, which the chapter READMEs in this repository follow:

| Element | Question it answers |
|---|---|
| Pattern overview | What problem does it solve, and how? |
| Practical applications | Where is it useful? |
| Hands-on example | What does a runnable implementation look like? Here, a C# port on Microsoft Agent Framework. |
| Key takeaways / at a glance | What, why, and the rule of thumb for when to apply it. |
| References | Where can I learn more? |

## Why cluster the patterns?

The book orders its chapters for learning, roughly from simple to advanced. The 21 patterns operate at different architectural levels, though. Some decide how work flows, some add capabilities, and some are production controls. A flat list of 21 items is hard to reason about when designing a system.

Grouping the patterns by the **architectural question they answer** gives a smaller mental model:

- Easier to choose: when you identify which question is unresolved, you can narrow the search to a handful of patterns.
- Easier to compose: real systems combine one or more patterns from several families.
- Easier to navigate: the .NET solution mirrors the families, so related demos sit next to each other.

Several groupings are possible: three architectural levels, six layers, or the book's own parts. This repository uses the **five families** from the research report [Agentic Design Patterns with Microsoft Foundry and Microsoft Agent Framework](../ai-research/agentic-design-patterns-report-1.md). They are memorable, map one-to-one to folders, and suit teaching.

## The five pattern families

| # | Family | Guiding question | Patterns | Folder |
|---|---|---|---|---|
| 1 | **Flow** | How does work move through the system? | Prompt Chaining (1), Routing (2), Parallelization (3), Planning (6), Prioritization (20), *Decision Models* | [`dotnet/src/01-Flow`](../dotnet/src/01-Flow/README.md) |
| 2 | **Reason & Improve** | How does the agent think about and improve its work? | Reflection (4), Learning & Adaptation (9), Goal Setting & Monitoring (11), Reasoning (17), Exploration & Discovery (21) | [`dotnet/src/02-ReasonAndImprove`](../dotnet/src/02-ReasonAndImprove/README.md) |
| 3 | **Knowledge & Action** | What does the agent know, and what can it do? | Tool Use (5), Memory (8), MCP (10), RAG (14) | [`dotnet/src/03-KnowledgeAndAction`](../dotnet/src/03-KnowledgeAndAction/README.md) |
| 4 | **Collaboration** | Who participates in the work? | Multi-Agent Collaboration (7), Human-in-the-Loop (13), A2A (15) | [`dotnet/src/04-Collaboration`](../dotnet/src/04-Collaboration/README.md) |
| 5 | **Trust & Operations** | How does it stay safe, reliable, and affordable in production? | Exception Handling (12), Resource Optimization (16), Guardrails (18), Evaluation & Monitoring (19), *Agent Harness* | [`dotnet/src/05-TrustAndOperations`](../dotnet/src/05-TrustAndOperations/README.md) |

Numbers in brackets are book chapters. Items in *italics* are repository additions beyond the book.

### 1. Flow

Flow patterns define the **shape of execution**: the step order, which branch runs, what runs concurrently, and what comes first.

- **Use when** a task has identifiable steps or categories, independent sub-tasks, or competing work items.
- **Watch for** serial latency in chains, misrouting, and aggregation conflicts after fan-out. Unbounded plans are another risk.
- **Repository note:** Decision Models belongs here. It supplies typed, calibrated decisions that drive routing, ranking, and gating.

### 2. Reason & Improve

These patterns spend extra inference to **raise the quality** of an answer. They use structured reasoning, critique-and-revise loops, explicit goals, feedback-driven adaptation, and hypothesis exploration.

- **Use when** correctness or quality matters more than latency, and success criteria can be stated.
- **Watch for** loops without stop conditions, a critic that shares the generator's blind spots, and rising cost.

### 3. Knowledge & Action

These patterns connect the model to the world. They **ground** it in data through RAG and memory, and let it **act** through tools and MCP.

- **Use when** the answer depends on private, current, or remembered information, or when the agent must change state in another system.
- **Watch for** irrelevant retrieval, stale memory, and over-broad tools. Every tool expands the attack surface.

### 4. Collaboration

Collaboration patterns distribute work across **multiple actors**. These can be specialised agents, remote agents reached over a protocol, or people who approve and correct.

- **Use when** the problem decomposes into roles with different instructions, tools, or models, or when a decision needs human accountability.
- **Watch for** coordination overhead, lost context between agents, and approval fatigue.

### 5. Trust & Operations

These patterns turn an agent into a **production system**. They bound behaviour, recover from failures, control cost, measure quality, and package the runtime.

- **Use when** always, in proportion to risk. This family applies to every other family.
- **Repository note:** Agent Harness, an enterprise extension, belongs here because it packages operational controls (todos, bounded tools, compaction, tracing, and completion checks) around an agent.

## Control before autonomy

The central lesson of the research is **control before autonomy**. Start with the smallest controllable system and add autonomy only when evidence shows it is needed.

```text
One agent → typed tools → explicit limits → tracing → evaluation
```

A useful complexity progression is:

```text
Single agent
  → deterministic workflow            (Flow)
  → reflection / verification         (Reason & Improve)
  → routing / parallelization         (Flow)
  → planner–worker                    (Flow + Collaboration)
  → dynamic multi-agent system        (Collaboration)
```

Treat multi-agent architecture as an **optimisation for decomposition**, not a default. Trust & Operations controls should be in place before you move up the progression.

## Composing patterns

Real systems combine families. For example, a customer-support assistant might use:

1. **Guardrails** (Trust & Operations) to screen the incoming request.
2. **Routing** (Flow) to classify it as billing, technical, or general.
3. **RAG** and **Tool Use** (Knowledge & Action) to ground the answer and look up the order.
4. **Reflection** (Reason & Improve) to check the draft against policy.
5. **Human-in-the-Loop** (Collaboration) to approve refunds above a threshold.
6. **Exception Handling** and **Evaluation** (Trust & Operations) to recover from tool failures and track quality.

When designing, ask the five guiding questions in turn. Any question that has no clear answer points to the family to look at next.

## Mapping to Microsoft Agent Framework and Foundry

| Family | Microsoft Agent Framework building blocks | Microsoft Foundry capabilities |
|---|---|---|
| Flow | `WorkflowBuilder`, executors, conditional and switch edges, sequential and concurrent orchestration | Hosted agents, model deployments |
| Reason & Improve | Agent loops in workflows, structured output, middleware | Reasoning models, evaluation |
| Knowledge & Action | Function tools, MCP tools, context providers, agent sessions | Toolbox, MCP integration, file search, memory, Bing grounding |
| Collaboration | Handoff, group chat, Magentic orchestration, `RequestPort` for human input, A2A | Agent Service, A2A and Invocations protocols |
| Trust & Operations | Middleware, checkpoints, OpenTelemetry tracing, the agent harness | Guardrails and content safety, evaluation, tracing, Application Insights, identity |

The [.NET guide](../dotnet/README.md) lists every demo, hosted companion, and test project by family.

## Proposed contracts by family

The [enterprise backlog](../ai-research/agentic-patterns-backlog.md#backlog-by-pattern-family) uses these same families. Primary ownership follows the missing contract; related-family links show composition rather than duplicate work.

| Family | Primary backlog entries | Related entries |
|---|---|---|
| Flow | None | B02, B03, B10 |
| Reason & Improve | None | B09, B11 |
| Knowledge & Action | B04 Governed Skill Packages; B05 Context Lifecycle and Verified Handoff; B06 Memory Write and Lifecycle Governance | B01, B02, B07, B08 |
| Collaboration | None | B01, B03, B05, B08 |
| Trust & Operations | B01 Safe-Action Gateway; B02 Transactional Actuator; B03 Durable Reactive Agent; B07 Capability-Scoped Tool Authority; B08 Sandbox and Promote; B09 Trajectory Recording and Offline Replay; B10 Admission Control, Backpressure, and Bulkheads; B11 EvalOps and Controlled Rollout | B04, B05, B06 |

These are proposed contracts, not implemented demos. Empty primary categories reflect the backlog's enterprise focus, not a need to invent more patterns. The [taxonomy crosswalk and related notes](../ai-research/agentic-patterns-backlog.md#related-concepts-and-notes-for-existing-patterns) explain how the Avramovic catalog overlaps with this repository, including Fusion, Awareness, Spec-First, and completion controls.

## Further reading

- [`book/Agentic_Design_Patterns_Complete.md`](../book/Agentic_Design_Patterns_Complete.md) and the [PDF](../book/Agentic_Design_Patterns_Complete.pdf): the source book.
- [Agentic Design Patterns with Microsoft Foundry and Microsoft Agent Framework](../ai-research/agentic-design-patterns-report-1.md): the research report that defines the five families and the MAF/Foundry mapping.
- [Agent Harness pattern research](../ai-research/agent-harness-pattern.md): background for the enterprise extension.
