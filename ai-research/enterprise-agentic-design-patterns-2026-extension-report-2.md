# Enterprise Agentic Design Patterns — 2026 Extension

**Focus:** Production-ready and near-production agent architectures  
**Primary implementation target:** Microsoft Agent Framework with C#  
**Perspective:** Vendor-neutral pattern discovery with Microsoft implementation mapping

---

# Executive Summary

The next generation of agentic design patterns is less about inventing new reasoning loops and more about turning agents into **reliable distributed software systems**.

The strongest additions beyond the original Agentic Design Patterns taxonomy are:

1. Bounded Agentic Islands
2. Agent Harness
3. Durable Reactive Agents
4. Safe-Action Gateway
5. Transactional Actuator
6. Capability-Scoped Agent Identity
7. Sandbox and Promote
8. Governed Skills
9. Context Lifecycle
10. Memory Lifecycle Governance
11. Heterogeneous Model Routing
12. Evaluator-Contract Loops
13. EvalOps and Controlled Rollout
14. Bounded-Autonomy Envelopes

The architectural shift can be summarized as:

```text
2023–2024

Prompt
 ↓
LLM
 ↓
Tools
 ↓
Agent Loop
```

becoming:

```text
2026 Enterprise Architecture

Business Workflow
       ↓
Deterministic Control Plane
       ↓
Agent Harness
       ↓
Bounded Agentic Islands
       ↓
Safe Action Boundary
       ↓
Isolated Execution
       ↓
Enterprise Systems
```

The most important principle is:

> **The agent should not be the application. The agent should be a bounded reasoning component inside the application.**

---

# Revised Taxonomy

## Layer 1 — Business and Deterministic Control

Patterns:

- Bounded Agentic Island
- Typed Workflow Graph
- State Machine
- Event-Driven Agent
- Durable Workflow
- Budget Envelope

Responsibilities:

- Scheduling
- State transitions
- Policy
- Retry
- Timeouts
- Budgets
- Business rules

---

# Layer 2 — Agent Harness

The harness provides the operating environment around the model.

Components:

```text
Agent Harness
├── System Instructions
├── Tool Registry
├── Context Assembly
├── Memory
├── Skills
├── Planning / Todo State
├── Permissions
├── Checkpoints
├── Retry Policy
├── Approval Policy
├── Budgets
├── Tracing
├── Evaluation
└── Termination
```

The harness should be treated as a **meta-pattern and architectural runtime layer**.

---

# Layer 3 — Agentic Islands

Examples:

- Router
- Planner–Worker
- Generator–Verifier
- Research Agent
- Model Router
- Specialist Agent
- Magentic Manager

These components are intentionally bounded.

---

# Layer 4 — Action Boundary

Patterns:

- Safe-Action Gateway
- Propose-Then-Execute
- Approval Gate
- Transactional Actuator
- Idempotent Action
- Saga / Compensation
- Read/Write Tool Separation

---

# Layer 5 — Execution Environment

Patterns:

- Sandbox per task
- Ephemeral execution
- Computer-use isolation
- Code execution isolation
- Network restrictions
- Filesystem restrictions
- Resource limits

---

# Layer 6 — Runtime and Operations

Patterns:

- Durable scheduler
- Checkpoint store
- Event bus
- Telemetry
- Evaluation
- Shadow deployment
- Canary deployment
- Model/version pinning
- Rollback

---

# 1. Agent Harness

Implementation companion: [focused research and source references](agent-harness-pattern.md), [runnable MAF extension](../dotnet/src/05-TrustAndOperations/Extension01.AgentHarness/README.md), and [Foundry hosted agent](../dotnet/src/05-TrustAndOperations/Extension01.AgentHarness.Hosted/README.md). The implementation uses the real `Microsoft.Agents.AI.Harness` package and distinguishes application-owned MAF harnesses from Foundry-managed harness products.

## Classification

**Meta-pattern / runtime architecture**

## Enterprise readiness

**Mature preview / rapidly converging production architecture**

## Problem

A raw LLM/tool loop lacks:

- lifecycle management
- planning state
- context management
- permissions
- memory discipline
- approvals
- termination
- observability

## Solution

Place the model inside a runtime harness.

```text
              ┌───────────────────────┐
              │      Agent Harness    │
              │                       │
Request ─────→│ Context               │
              │ Skills                │
              │ Memory                │
              │ Tools                 │
              │ Planning              │
              │ Permissions           │
              │ Budgets               │
              │ Checkpoints           │
              │ Evaluation            │
              │ Termination           │
              └───────────┬───────────┘
                          ↓
                         LLM
```

## Why this matters

The model becomes replaceable.

The harness owns operational behavior.

This is analogous to the relationship between:

```text
Application Code
      +
Application Runtime
```

rather than treating the model as the runtime.

## Microsoft Agent Framework

Microsoft Agent Framework exposes a Harness Agent abstraction that combines capabilities such as:

- planning
- todo tracking
- context compaction
- skills
- memory
- approvals
- bounded loops
- observability

Conceptually:

```csharp
AIAgent agent = chatClient.AsHarnessAgent(
    new HarnessAgentOptions
    {
        Name = "enterprise-worker",
        MaxContextWindowTokens = 64_000,
        AIContextProviders = providers
    });
```

---

# 2. Bounded Agentic Island

## Enterprise readiness

**Production-ready architecture**

## Problem

Allowing an LLM to control an entire business process makes the system:

- nondeterministic
- difficult to audit
- difficult to test
- difficult to recover

## Solution

Place agentic reasoning inside deterministic boundaries.

```text
Deterministic
    ↓
Agentic
    ↓
Deterministic
    ↓
Agentic
    ↓
Deterministic
```

Example:

```text
Validate Request
      ↓
Agent Classifies Risk
      ↓
Business Rule
      ↓
Agent Drafts Recommendation
      ↓
Policy Engine
      ↓
Human Approval
      ↓
Deterministic Execution
```

## Key rule

> Agents decide where judgment is required. Code decides where correctness is required.

## Microsoft Agent Framework

Use:

- `WorkflowBuilder`
- Typed messages
- Executors
- Conditional edges
- Switch edges
- Structured outputs
- `RequestPort`

---

# 3. Durable Reactive Agent

## Enterprise readiness

**Underlying architecture: production-ready**  
**Some Agent Framework integrations: preview**

## Problem

Agent work may last:

- minutes
- hours
- days

and may wait for:

- humans
- external systems
- timers
- events
- queues

## Solution

Use durable execution.

```text
Event
 ↓
Agent Step
 ↓
Checkpoint
 ↓
Wait
 ↓
External Event
 ↓
Resume
 ↓
Agent Step
```

## Required properties

- Correlation IDs
- Checkpoints
- Durable state
- Idempotency
- Retry policy
- Dead-letter handling
- Cancellation
- Timeout
- Compensation

## Important distributed-systems reality

Do not design around exactly-once execution.

Design around:

> **At-least-once delivery + idempotent processing.**

## Microsoft implementation

Use:

- Agent Framework workflows
- Checkpoints
- Durable Task
- Azure Functions
- External events
- Timers

---

# 4. Safe-Action Gateway

## Enterprise readiness

**Production-ready**

## Problem

Agents can propose incorrect or dangerous mutations.

Examples:

- Delete record
- Send email
- Change configuration
- Create purchase order
- Modify permissions

## Solution

Separate reasoning from execution.

```text
Agent
 ↓
Action Proposal
 ↓
Canonicalization
 ↓
Validation
 ↓
Policy
 ↓
Approval
 ↓
Execution
```

## Strong enterprise rule

Never expose:

```text
DeleteCustomer(id)
```

directly to a broadly autonomous agent if you can expose:

```text
ProposeCustomerDeletion(id, reason)
```

followed by deterministic authorization.

## Microsoft Agent Framework

Use:

- Approval-required functions
- Middleware
- `RequestPort`
- Durable human waits

---

# 5. Transactional Actuator

## Enterprise readiness

**Production-ready**

This is primarily a distributed-systems pattern applied to agents.

## Problem

An agent performs:

```text
Create Order
 ↓
Charge Customer
 ↓
Send Confirmation
```

and fails after step two.

## Solution

Use:

- Idempotency keys
- Action ledger
- Transaction boundaries
- Optimistic concurrency
- Compensation

Example:

```text
Create Order
 ↓
Charge
 ↓
FAIL
 ↓
Compensate Charge
 ↓
Cancel Order
```

## Important rule

Compensation logic should be deterministic.

Do not ask the LLM:

> “What should we undo?”

The application should already know.

---

# 6. Capability-Scoped Agent Identity

## Enterprise readiness

**Production-ready**

## Problem

A shared service account gives every agent excessive authority.

## Solution

Use:

```text
Agent
 ↓
Dedicated Identity
 ↓
Scoped Permissions
 ↓
Specific Resources
```

Separate:

- Read identity
- Write identity
- User-delegated identity
- Autonomous service identity

## Principles

- Least privilege
- Short-lived credentials
- Explicit scopes
- Tenant isolation
- No secrets in prompts

## Microsoft mapping

Use:

- Microsoft Entra ID
- Managed Identity
- Foundry Hosted Agent identity
- Azure RBAC

---

# 7. Sandbox and Promote

## Enterprise readiness

**Production-ready architecture**

## Problem

Code execution and computer use require powerful capabilities.

## Solution

Execute in an isolated environment.

```text
Agent
 ↓
Ephemeral Sandbox
 ↓
Generate / Execute
 ↓
Artifact
 ↓
Tests
 ↓
Verifier
 ↓
Approval
 ↓
Promotion
```

## Sandbox controls

- Ephemeral filesystem
- Restricted network
- CPU limits
- Memory limits
- Time limits
- No production credentials
- Audit logs

## Key distinction

Sandboxing protects execution.

Approval protects promotion.

You usually need both.

---

# 8. Governed Skill Package

## Enterprise readiness

**Production-ready core; ecosystem standards still evolving**

## Problem

Procedural knowledge is repeatedly embedded in prompts.

## Solution

Package reusable procedures as Skills.

```text
Skill
├── Metadata
├── Instructions
├── References
├── Templates
├── Scripts
└── Assets
```

## Progressive disclosure

Do not load every skill into every context.

Instead:

```text
Skill Metadata
      ↓
Agent selects relevant skill
      ↓
Load instructions
      ↓
Load resources only if required
```

## Skills versus other concepts

| Concept | Purpose |
|---|---|
| Prompt | Immediate behavior |
| RAG | Retrieve facts |
| Memory | Remember experience |
| Tool | Perform action |
| MCP | Capability interoperability |
| Skill | Reusable procedure |

## Enterprise governance

Skills should have:

- Owner
- Version
- Tests
- Provenance
- Approved tools
- Compatibility metadata
- Security classification
- Signature/digest
- Sandbox requirements

## Microsoft Agent Framework

Use:

- `AgentSkillsProvider`
- File-based skills
- Code-based skills
- Skill filtering
- Skill caching

---

# 9. Context Lifecycle

## Enterprise readiness

**Production-ready**

## Problem

Unlimited context causes:

- cost
- latency
- distraction
- stale information
- context poisoning

## Solution

Treat context as a lifecycle.

```text
Acquire
 ↓
Classify
 ↓
Select
 ↓
Use
 ↓
Compact
 ↓
Offload
 ↓
Archive / Delete
```

## Context categories

### Working context

Current task.

### Retrieved context

External evidence.

### Delegated context

Information passed to another agent.

### Archived context

Information no longer required for inference.

## Context handoff

Prefer:

```json
{
  "goal": "...",
  "completed": [],
  "evidence": [],
  "openQuestions": [],
  "artifacts": [],
  "nextAction": "..."
}
```

instead of passing an entire agent transcript.

---

# 10. Memory Lifecycle Governance

## Enterprise readiness

**Production-ready**

## Memory taxonomy

```text
Working Memory
Episodic Memory
Semantic Memory
Procedural Memory
```

## Critical addition

Memory requires a **write policy**.

Do not automatically store everything the model says.

Use:

```text
Candidate Memory
      ↓
Validation
      ↓
Provenance
      ↓
Classification
      ↓
Store
```

## Lifecycle

```text
Write
 ↓
Validate
 ↓
Use
 ↓
Consolidate
 ↓
Expire
 ↓
Delete
```

## Enterprise requirements

- Tenant isolation
- User isolation
- Provenance
- Confidence
- TTL
- Deletion
- Auditability

---

# 11. Heterogeneous Model Router

## Enterprise readiness

**Mature preview**

## Problem

Using the strongest model for every task is expensive.

## Solution

Route based on:

- Capability
- Complexity
- Cost
- Latency
- Privacy
- Context size
- Availability

```text
Task
 ↓
Model Router
 ├─ Local SLM
 ├─ Fast LLM
 ├─ Reasoning Model
 └─ Specialist Model
```

## Enterprise rule

Do not let the model decide its own competence without independent evidence.

Use:

- Task classifier
- Business rules
- Verifier
- Historical evaluation data

---

# 12. Evaluator-Contract Loop

## Enterprise readiness

**Mature preview / production-relevant**

## Problem

Self-reflection is not independent quality assurance.

## Solution

Define the acceptance contract before generation.

```text
Acceptance Contract
       ↓
Generator
       ↓
Artifact
       ↓
Verifier
       ↓
Pass?
 ┌─────┴─────┐
Yes          No
 ↓            ↓
Done        Retry
```

## Acceptance criteria can include

- JSON schema
- Unit tests
- Business rules
- Required citations
- Completeness
- Policy compliance
- LLM evaluator

## Important rule

Use deterministic verification wherever possible.

LLM-as-judge should supplement—not replace—deterministic checks.

---

# 13. EvalOps and Controlled Rollout

## Enterprise readiness

**Production-ready**

Agent deployment should resemble software deployment.

```text
Change
 ↓
Unit Tests
 ↓
Golden Dataset
 ↓
Trajectory Evaluation
 ↓
Preview
 ↓
Shadow
 ↓
Canary
 ↓
Production
 ↓
Online Evaluation
```

## Golden trajectories

Store representative traces containing:

- Input
- Expected tool selection
- Expected constraints
- Expected output properties
- Expected stop condition

## Shadow agents

Run the new agent without allowing actions.

Compare:

```text
Production Agent
       vs
Candidate Agent
```

## Canary

Enable the candidate for:

- Internal users
- Selected tenants
- Small traffic percentage

Then expand gradually.

---

# 14. Bounded-Autonomy Envelope

## Enterprise readiness

**Production-ready architectural control**

Every autonomous agent should have explicit limits.

Example:

```json
{
  "maxSteps": 20,
  "maxToolCalls": 30,
  "maxWriteActions": 2,
  "maxCost": 1.50,
  "maxRuntimeMinutes": 10,
  "requiresApprovalFor": [
    "delete",
    "send",
    "purchase"
  ]
}
```

## Envelope dimensions

- Token budget
- Cost budget
- Time budget
- Tool-call budget
- Retry budget
- Write-action budget
- Data-access scope
- Network scope

---

# Protocol Patterns

## MCP

MCP standardizes agent-to-tool interoperability.

```text
Agent
 ↓
MCP Client
 ↓
MCP Server
 ↓
Tools / Resources
```

MCP is not an architecture.

It still requires:

- Identity
- Authorization
- Policy
- Timeout
- Audit
- Tool scoping

---

# A2A

A2A standardizes remote agent interoperability.

```text
Agent A
 ↓
A2A
 ↓
Agent B
```

Use when agents have independent:

- Deployment
- Ownership
- Identity
- Lifecycle

Do not use A2A merely because two classes in the same application are called “agents.”

---

# Enterprise Decision Matrix

| Pattern | Enterprise Value | Maturity | Reliability | Security | Complexity | MAF Fit |
|---|---|---|---|---|---|---|
| Bounded Agentic Island | High | Production | High | High | Medium | High |
| Safe-Action Gateway | High | Production | High | High | Medium | High |
| Durable Reactive Agent | High | Mature Preview | High | Medium | High | High |
| Transactional Actuator | High | Production | High | High | High | Medium |
| Capability-Scoped Identity | High | Production | Medium | High | Medium | High |
| Agent Harness | High | Mature Preview | High | Medium | Medium | High |
| Sandbox and Promote | High | Production | High | High | High | Medium |
| Governed Skills | High | Production Core | Medium | Medium | Medium | High |
| Context Lifecycle | High | Production | High | Medium | Medium | High |
| Memory Lifecycle | High | Production | High | High | High | Medium |
| Evaluator Contract | High | Mature Preview | High | Medium | Medium | High |
| EvalOps | High | Production | High | High | High | High |
| Model Router | Medium–High | Mature Preview | Medium–High | Medium | Medium | High |

---

# Microsoft Agent Framework Implementation Blueprint

## Harness with Skills

```csharp
var skills = new AgentSkillsProviderBuilder()
    .UseFileSkill("approved-skills")
    .UseFilter((skill, ctx) =>
        approved.Contains(skill.Frontmatter.Name))
    .Build();

AIAgent agent = chatClient.AsHarnessAgent(
    new HarnessAgentOptions
    {
        Name = "enterprise-worker",
        MaxContextWindowTokens = 64_000,
        AIContextProviders = [skills]
    });
```

---

# Deterministic Workflow Around Agentic Decisions

```csharp
record Assessment(
    RiskLevel Risk,
    string Rationale);

var workflow = new WorkflowBuilder(validateInput)
    .AddEdge(validateInput, assessmentAgent)
    .AddSwitch(
        assessmentAgent,
        s => s
            .AddCase(
                x => ((Assessment)x!).Risk == RiskLevel.Low,
                policyGate)
            .WithDefault(humanReview))
    .AddEdge(policyGate, idempotentActuator)
    .Build();
```

The important architecture is:

```text
Agent
 ↓
Typed Assessment
 ↓
Deterministic Transition
```

not:

```text
Agent
 ↓
"Do whatever you think is best"
```

---

# Durable Approval

Conceptual architecture:

```csharp
var proposal =
    await context.GetAgent("Planner")
        .RunAsync<ActionProposal>(request);

await context.CallActivityAsync(
    "PersistProposal",
    proposal);

var decision =
    await context.WaitForExternalEvent<ApprovalDecision>(
        "ApprovalDecision",
        timeout: TimeSpan.FromHours(24));

if (!decision.Approved)
{
    return "Rejected";
}

return await context.CallActivityAsync<ActionResult>(
    "ExecuteIdempotently",
    new(
        proposal,
        IdempotencyKey: context.InstanceId));
```

---

# Safe Action Architecture

```text
Agent
 ↓
ActionProposal
 ↓
Schema Validation
 ↓
Policy Engine
 ↓
Risk Classification
 ├─ Read → Execute
 ├─ Reversible Write → Execute + Audit
 └─ Irreversible Write → Human Approval
                              ↓
                           Execute
```

---

# Sandbox Architecture

```text
Agent
 ↓
Create Sandbox
 ↓
Execute Code
 ↓
Generate Artifact
 ↓
Run Tests
 ↓
Security Scan
 ↓
Verifier
 ↓
Approval
 ↓
Promote Artifact
```

---

# Anti-Patterns

## Agent Owns Entire Business Process

### Avoid

```text
User → Autonomous Agent → Enterprise
```

### Prefer

```text
Workflow
 ↓
Bounded Agent
 ↓
Workflow
 ↓
Safe Action Gateway
```

---

## Protocol Equals Architecture

MCP and A2A solve interoperability.

They do not solve:

- authorization
- orchestration
- durability
- evaluation
- transaction safety

---

## Retry the Entire Agent

Prefer checkpointed activity-level retry.

---

## Direct Production Writes

Prefer:

```text
Propose
 ↓
Validate
 ↓
Authorize
 ↓
Execute
```

---

## Shared Mutable Agent Memory

Prefer immutable artifacts with explicit ownership.

---

## Agent per Persona

Do not create:

- Researcher Agent
- Friendly Agent
- Senior Agent
- Reviewer Agent

unless they genuinely differ in:

- tools
- permissions
- context
- model
- responsibility

---

## Giant Prompt

Prefer:

- Skills
- RAG
- Context routing
- Progressive disclosure
- Compaction

---

## Self-Reflection as QA

Prefer independent verification.

---

## Untrusted Skill Scripts

Require:

- Review
- Version
- Signature
- Sandbox
- Approval

---

## Omnipotent Service Account

Use capability-scoped identities.

---

## Unlimited Autonomous Loop

Always define:

- Step ceiling
- Token ceiling
- Cost ceiling
- Runtime ceiling
- Tool-call ceiling
- Write-action ceiling

---

# Recommended Enterprise Standard

## 1. Workflow owns control

Agents provide judgment.

Code owns transitions.

## 2. Typed contracts everywhere

Every agent-to-code boundary should use structured output.

## 3. Classify tools

At minimum:

```text
READ
REVERSIBLE_WRITE
IRREVERSIBLE_WRITE
```

## 4. Require idempotency

Every write should have:

- Correlation ID
- Idempotency key
- Audit record

## 5. Dedicated identities

Use least privilege.

## 6. Durable execution

Mandatory when work outlives one request.

## 7. Sandbox code and computer use

Never execute arbitrary model-generated code directly in production.

## 8. Govern Skills

Treat Skills like software dependencies.

## 9. Observability by default

Capture:

- Agent
- Model
- Tool
- Route
- Latency
- Errors
- Retry
- Approval
- Stop reason

## 10. Evaluation gates

No prompt/model/skill change should bypass evaluation.

## 11. Progressive autonomy

Recommended maturity model:

```text
Level 0
Assistant only

Level 1
Recommendation

Level 2
Shadow execution

Level 3
Reversible autonomous actions

Level 4
Bounded write actions

Level 5
High-impact actions with approval
```

## 12. Always provide an escape route

Every agent needs:

- Timeout
- Circuit breaker
- Safe stop
- Human escalation
- Rollback

---

# Recommended Talk Structure

## Pattern 1 — Agent Harness

Show the difference between:

```text
LLM + Tools
```

and:

```text
Harness
├── Context
├── Skills
├── Memory
├── Planning
├── Tools
├── Approval
├── Budgets
└── Evaluation
```

---

## Pattern 2 — Bounded Agentic Island

Show one LLM decision inside a deterministic workflow.

This is probably the most important enterprise pattern.

---

## Pattern 3 — Skills

Demonstrate progressive disclosure.

Show context before and after skill activation.

---

## Pattern 4 — Durable Agent

Start a workflow.

Stop the application.

Resume it.

Then wait for human approval.

This makes durability tangible.

---

## Pattern 5 — Sandbox and Promote

Agent generates code or an artifact.

Tests—not the agent—decide whether it can progress.

---

## Pattern 6 — Evaluator Contract

Show:

```text
Generator
 ↓
Verifier
 ↓
Retry
```

with explicit acceptance criteria.

---

# Forward-Looking Watch List

## Converging into durable practice

The following patterns appear likely to survive framework and model changes:

- Agent Harness
- Bounded Agentic Islands
- Deterministic + Agentic Hybrid Architecture
- Durable Execution
- Human Waits
- Skills
- Context Lifecycle
- Capability-Scoped Identity
- Sandboxed Execution
- Safe-Action Gateways
- Trace-Based Evaluation
- Controlled Rollout
- Caller-Managed Context

---

# Still Unsettled

Watch:

- Skill signing standards
- Skill registries
- Cross-vendor skill trust
- Skills over MCP
- Portable memory formats
- Memory write-validation standards
- A2A discovery
- Agent attestation
- Cross-provider model routing
- Computer-use safety contracts
- Cross-framework trajectory semantics
- Standardized agent policy hooks

---

# Strategic Conclusion

Do not standardize primarily on:

- a particular agent topology
- a particular model
- a particular orchestration framework
- a particular vendor-specific agent type

Standardize on:

```text
Contracts
Harness Responsibilities
Context Lifecycle
Memory Lifecycle
Identity
Durability
Action Safety
Evaluation
Observability
Deployment Evidence
```

These architectural boundaries are much more likely to survive changes in models and frameworks.

The central enterprise architecture principle is:

> **Deterministic control should surround bounded probabilistic reasoning.**

Or, more simply:

> **Let the model think. Let software remain in control.**