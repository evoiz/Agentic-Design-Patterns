# Agentic Patterns Backlog

> Proposed enterprise extensions beyond the repository's implemented patterns. These are future C#/.NET Microsoft Agent Framework (MAF) demos, not additional Antonio Gulli book chapters.

## Scope and evidence

This backlog covers **patterns not yet implemented as complete demonstrations**, not improvements to existing chapter demos. Related building blocks may already exist; each entry identifies the additional behavioral contract that makes it distinct.

Research and repository coverage were reviewed on **27 October 2026**, using:

- [Original pattern research](agentic-design-patterns-report-1.md): taxonomy, newer patterns, and research synthesis.
- [Enterprise extension research](enterprise-agentic-design-patterns-2026-extension-report-2.md): production architecture candidates and controls.
- [Agent Harness research](agent-harness-pattern.md): implemented boundaries, combinations, and source references.
- The [repository guide](../README.md), [.NET guide](../dotnet/README.md), relevant demo guides, central package versions, and targeted C# source searches.
- Eleven additional primary sources, listed under [Sources and evidence limits](#sources-and-evidence-limits).

Selection rule: a new entry needs a distinct missing behavioral contract, a runnable scenario, and observable acceptance criteria. Renaming an existing composition does not make it new.

This is a repository-gap assessment, not an exhaustive audit. **Existing** means executable behavior is already demonstrated; **adjacent** means building blocks exist but the proposed contract is missing; **proposed** means backlog work, not an implementation or production-readiness claim.

The deliverable is this backlog only. It does not imply creating GitHub issues, implementing all entries, installing dependencies, provisioning Azure resources, or deploying agents.

## Existing coverage: do not duplicate

| Candidate from the research | Repository evidence | Backlog decision |
|---|---|---|
| Original 21 book patterns | [.NET chapter catalog](../dotnet/README.md#chapter-demos) | Exclude as new patterns. |
| Agent Harness | [Extension 01](../dotnet/src/Extension01.AgentHarness/README.md) and its hosted companion | Exclude. Includes todos, bounded calls, compaction, tracing, and evidence checks already. |
| Bounded Agentic Island | [Decision Models](../dotnet/src/DecisionModels/README.md): typed judgments inside deterministic policy and workflow transitions | Exclude a standalone duplicate. Reuse this architecture in future extensions. |
| Heterogeneous model routing | [Chapter 16](../dotnet/src/Chapter16.ResourceOptimization/README.md) selects fast/reasoning/quality deployments; Decision Models adds specialized judgment models | Exclude generic model routing. Local-first privacy enforcement remains a possible later distinct variant. |
| Generic bounded autonomy | Harness and chapter workflows enforce call, iteration, and deadline limits | Exclude another basic budget-loop demo. Cross-request admission/backpressure is a separate gap, B10. |
| Basic approval and restartable approval sessions | [Chapter 13](../dotnet/src/Chapter13.HumanInTheLoop/README.md) uses approval-required functions. Its [hosted companion](../dotnet/src/Chapter13.HumanInTheLoop.Hosted/README.md) atomically persists caller-partitioned pending approvals and can resume after process restart | Approval persistence is not wholly absent. B01 adds business authorization and freshness; B03 adds distributed orchestration and event recovery. |
| Basic memory, scope cleanup, and compaction | [Chapter 08](../dotnet/src/Chapter08.Memory/README.md) has session serialization and managed-memory scope cleanup; Harness has compaction | Exclude basic remembering, deletion, or summarization. B05/B06 require explicit handoff and governed write/correction contracts. |
| Reflection, structured output validation, and judging | [Chapter 04](../dotnet/src/Chapter04.Reflection/README.md), [Chapter 11](../dotnet/src/Chapter11.GoalSettingMonitoring/README.md), [Chapter 19](../dotnet/src/Chapter19.Evaluation/README.md), and Harness completion checks | Do not add a generic generator-verifier loop. B09/B11 concern replayable trajectories and release decisions instead. |
| Code Interpreter | [Chapter 05](../dotnet/src/Chapter05.ToolUse/README.md) and [Chapter 17](../dotnet/src/Chapter17.Reasoning/README.md) already use hosted sandboxed computation | Exclude basic code execution. B08 adds isolated artifact verification and controlled promotion. |
| Managed identity and inference RBAC | [Hosted-agent guides](../dotnet/README.md#hosted-agent-companions) already explain runtime identity and model access | Exclude another managed-identity setup sample. B07 concerns per-resource/per-operation authority and confused-deputy prevention. |

## Ranked backlog

All entries are **proposed / not started**. B-numbers are stable backlog IDs, not chapter numbers or reserved extension numbers.

**P1:** strong next-extension candidate with clear teaching value, an identifiable gap, and a bounded first demo. **P2:** valuable follow-on work with greater infrastructure, governance, or evaluation complexity. Priority is a recommendation, not a measured benchmark or time estimate.

| ID | Priority | Pattern | Missing contract / teaching payoff | Hard prerequisite in this design | Hosting stance |
|---|---|---|---|---|---|
| [B01](#b01---safe-action-gateway) | P1 | Safe-Action Gateway | Bind approval and authorization to an exact, fresh business action | None | Suitable after authenticated caller binding |
| [B02](#b02---transactional-actuator) | P1 | Transactional Actuator | Safe retries, action ledger, and deterministic compensation for writes | B01 | Only with external durable state |
| [B03](#b03---durable-reactive-agent) | P1 | Durable Reactive Agent | Distributed resume, external events, timeout/cancellation, duplicate delivery | None | Durable worker companion; not a stateless hosted wrapper |
| [B04](#b04---governed-skill-packages) | P1 | Governed Skill Packages | Progressive loading from a pinned, approved skill set | None | Suitable for bundled, read-only approved skills |
| [B05](#b05---context-lifecycle-and-verified-handoff) | P1 | Context Lifecycle and Verified Handoff | Preserve task invariants and evidence across context replacement | None | Suitable with scoped artifact persistence |
| [B06](#b06---memory-write-and-lifecycle-governance) | P2 | Memory Write and Lifecycle Governance | Validate, correct, expire, and delete provenance-bearing memory | None | Only with authenticated memory ownership |
| [B07](#b07---capability-scoped-tool-authority) | P2 | Capability-Scoped Tool Authority | Enforce caller, tenant, audience, resource, and operation boundaries | None | Useful authenticated service companion |
| [B08](#b08---sandbox-and-promote) | P2 | Sandbox and Promote | Separate untrusted execution from verified artifact promotion | B01 | Separate isolated executor; agent hosting is not itself a sandbox |
| [B09](#b09---trajectory-recording-and-offline-replay) | P2 | Trajectory Recording and Offline Replay | Reproduce decisions and state transitions without repeating external effects | None | Local evaluation tooling first |
| [B10](#b10---admission-control-backpressure-and-bulkheads) | P2 | Admission Control, Backpressure, and Bulkheads | Bound aggregate concurrency and isolate overloaded dependencies | None | Useful worker/service companion |
| [B11](#b11---evalops-and-controlled-rollout) | P2 | EvalOps and Controlled Rollout | Gate version changes using trajectories, shadow runs, canaries, and rollback | B09 | Release/evaluation tooling around agents |

### B01 - Safe-Action Gateway

**Problem and novelty:** Chapter 13 already pauses and resumes approval-required tool calls. The missing demonstration is a business action whose exact payload, authenticated approver, policy version, expiration, and target-resource version are checked again at execution.

**Demo:** An agent proposes a synthetic support credit. Deterministic code canonicalizes the proposal, checks a fixed teaching policy, and presents the exact amount, account, and reason for authorized review. Changed or stale proposals require a new decision.

**Implementation todo:** Build a typed proposal/decision/receipt pipeline around MAF tools or workflow executors. Separate proposal generation from the executor's authority. Bind approval to a proposal digest, tenant/caller, expiration, and resource version. Keep accounts and credits synthetic.

**Acceptance:** An approved unchanged proposal produces a receipt; altered amount/account, expired approval, wrong approver, or changed resource version prevents execution. Rejection produces no write and no success claim. Duplicate-execution guarantees belong to B02.

**Sources:** Enterprise report sections 4 and 14; Chapter 13 and its hosted guide; S6 for resource-specific authority principles.

### B02 - Transactional Actuator

**Problem and novelty:** B01 determines whether an action may run; it does not make a multi-step write recoverable. Existing [Chapter 12 recovery](../dotnet/src/Chapter12.ExceptionHandling/README.md) is not a durable side-effect transaction.

**Demo:** Reserve synthetic stock, create an order, and schedule a confirmation. Inject failure between operations and recover using an action ledger and predefined compensating commands.

**Implementation todo:** Reuse B01's authorized action envelope. Persist idempotency keys, command payload digests, outcomes, and compensation progress in a local transactional store. Define explicit timeout/unknown-outcome reconciliation. Include an outbox/inbox boundary where state persistence and message dispatch cannot share a transaction.

**Acceptance:** Repeated delivery of the same key and payload does not repeat the committed business mutation; key reuse with a different payload is rejected. Crash/restart after an external commit but before receipt storage is reconciled using the receiver's idempotency/status contract. Failed compensation is persisted and surfaced for intervention.

**Boundary:** No claim of exactly-once execution across arbitrary external systems. Compensation is business-specific and may not restore the original state.

**Sources:** Enterprise report section 5; S3.

### B03 - Durable Reactive Agent

**Problem and novelty:** Chapter 12 uses in-memory recovery checkpoints, while Chapter 13 hosted already has single-instance file-backed approval persistence. Neither is the proposed distributed, event-driven orchestration contract.

**Demo:** Start a synthetic document-review workflow, persist the completed agent step, stop the worker, resume on another worker, and wait for an external review event or timer.

**Implementation todo:** Use the MAF Durable Extension if compatible with the pinned package set; otherwise make an explicit, reviewed dependency decision. Persist orchestration identity, event correlation, completed activities, deadlines, and termination state. Keep model calls and side effects outside deterministic replay code.

**Acceptance:** Restart preserves completed work without another model call for a recorded completed step. Duplicate and out-of-order review events do not advance the workflow twice. Unauthorized events, timeout, cancellation, and poison events produce explicit outcomes. Run a real restart scenario against a durable backend, not only an in-memory simulation.

**Boundary:** At-least-once activity execution still needs idempotent downstream effects; use B02 when composing this with consequential writes. B02 is not required for the initial read-only review demo.

**Sources:** Enterprise report section 3; S1.

### B04 - Governed Skill Packages

**Problem and novelty:** Harness explicitly disables skill discovery. The repository's [coding-assistant skill](../.github/skills/maf-pattern-demo/SKILL.md) helps the coding assistant; it is not a runtime skill-governance demo for the application agent.

**Demo:** A support agent advertises two approved skill summaries and loads only the matching runbook's detailed instructions and references.

**Implementation todo:** Verify C# `AgentSkillsProvider` support against MAF 1.22.0. Bundle an approved skill manifest with owner, version, digest, compatible runtime, and allowed capabilities. Enforce path and size limits and approved-source filtering in code.

**Acceptance:** The selected skill's instructions are loaded on demand, unrelated bodies are absent, modified/unapproved packages are refused, and a skill cannot acquire an unregistered capability. Missing or invalid skill content causes an explicit error.

**Boundary:** First version is instruction/reference-only. No arbitrary scripts, remote registries, or automatic skill installation. A digest pins content; it is not proof of publisher identity. Skill metadata is not an authorization mechanism.

**Sources:** Both research reports' Skills sections; S4 and S5.

### B05 - Context Lifecycle and Verified Handoff

**Problem and novelty:** Existing compaction reduces history, and [Chapter 07](../dotnet/src/Chapter07.MultiAgent/README.md) passes agent output downstream. The missing contract validates what must survive context replacement and who may retrieve the referenced artifacts.

**Demo:** An incident investigation reaches a controlled context boundary. A fresh session continues from a typed handoff containing goal, completed work, unresolved questions, evidence references, and next permitted action.

**Implementation todo:** Define a versioned handoff schema and deterministic required-field/invariant checks. Store large tool outputs as scoped, digest-addressed artifacts. Preserve trusted instructions separately from retrieved text; do not silently elevate tool output into policy.

**Acceptance:** A fresh session retrieves the same evidence and preserves required incident facts and constraints. Missing, stale, altered, or cross-scope references fail explicitly. Demonstrate a smaller selected context under the same token-estimation method and report quality separately; do not claim exact billing savings.

**Sources:** Enterprise report section 9; focused harness research; S2.

### B06 - Memory Write and Lifecycle Governance

**Problem and novelty:** Chapter 08 already remembers selected facts, scopes managed memory, and cleans temporary data. The missing pattern is an explicit write/correction lifecycle rather than automatic conversational recall.

**Demo:** An agent proposes saving a synthetic user preference. Code validates provenance and ownership, applies a retention rule, supports a corrected replacement, and makes expired or deleted entries unavailable.

**Implementation todo:** Start with an application-owned local store so TTL, correction, and deletion are inspectable. Separate candidate memories from accepted records. Include authenticated owner, source, timestamp, expiration, version, and deletion status; retrieve only after scope and freshness filters.

**Acceptance:** Unsupported/inferred claims are not silently stored; another tenant cannot retrieve or modify a record; expired and superseded facts are not returned; deletion remains effective after restart. Use a controllable clock for deterministic expiry tests.

**Boundary:** Distinguish removal from active retrieval from physical erasure in backups, logs, or external services. Do not promise managed-service semantics without verifying them.

**Sources:** Enterprise report section 10; Chapter 08 limitations; S2.

### B07 - Capability-Scoped Tool Authority

**Problem and novelty:** Managed identity for model inference already exists. The new contract prevents a tool service from using its broader service identity to satisfy an unauthorized caller request.

**Demo:** A MAF agent calls a synthetic account service whose read and update capabilities are separated. A caller allowed to read account A cannot update it or access account B by changing tool arguments.

**Implementation todo:** Put authority checks at the service boundary, derive ownership from authenticated claims rather than prompts, and validate operation, resource, tenant, token issuer/audience, and expiry. Provide deterministic synthetic-principal tests plus an optional explicit Entra integration.

**Acceptance:** Wrong tenant/resource, expired credentials, inappropriate audience, and insufficient scope are denied before the tool performs work. Valid scoped requests succeed. Tokens and secrets never enter model context or captured fixtures.

**Boundary:** Prompt-based tool selection and MCP interoperability do not confer permission. Do not pass an upstream token to a downstream resource that has a different audience.

**Sources:** Enterprise report section 6; existing hosted identity guides; S6.

### B08 - Sandbox and Promote

**Problem and novelty:** Existing Code Interpreter examples calculate results. This extension must demonstrate both execution isolation and a separate, approved promotion boundary.

**Demo:** Generate a synthetic report transformation inside an ephemeral executor, verify its artifact, then approve promotion of that exact artifact into a sample output store.

**Implementation todo:** Select a real OS/container/managed execution boundary with restricted filesystem and network access, no production credentials, and resource limits. Keep the promotion credential outside the executor. Reuse B01 to bind approval to an artifact digest and verification receipt.

**Acceptance:** Attempts to access outside files or disallowed network endpoints are blocked by the executor, not by instructions. Timeout/oversized output fails explicitly. Only a verified, approved artifact is promoted; modification after review invalidates approval.

**Boundary:** A separate process or a temporary directory alone is not a sandbox. Document supported host operating systems; unsupported environments must not silently run without isolation.

**Sources:** Enterprise report section 7; S7. The source describes another product's isolation model, not a drop-in Windows/.NET implementation.

### B09 - Trajectory Recording and Offline Replay

**Problem and novelty:** Existing offline tests already use stubs and fixtures. The new pattern is a versioned, end-to-end trajectory format with replay semantics, mismatch detection, and a guarantee that replay cannot repeat real external effects.

**Demo:** Record a synthetic tool-using task, then replay its captured model/tool observations through the deterministic application policy. Inject one changed tool result to demonstrate a policy regression.

**Implementation todo:** Record schemas, sequence/correlation identifiers, tool arguments/results, model observations, environment/policy versions, and terminal state. Use an opt-in synthetic capture mode, redaction, and retention. Replace effectful dependencies with strict replay adapters.

**Acceptance:** Offline replay needs neither credentials nor network and reaches the recorded deterministic terminal state. Unexpected calls, reordered dependencies, incompatible versions, or unmatched arguments fail explicitly. No live write adapter is reachable during replay.

**Boundary:** This replays recorded observations and deterministic control, not a promise that a fresh stochastic model produces identical text. A narrow trajectory log does not require migrating the whole application to event sourcing.

**Sources:** Independent research synthesis from S8 and S9, beyond a separately named pattern in the supplied reports.

### B10 - Admission Control, Backpressure, and Bulkheads

**Problem and novelty:** Parallelization and per-request budgets do not bound aggregate demand. Chapter 13's per-session lock is also not a service-level concurrency/admission policy.

**Demo:** Submit a burst of synthetic research jobs. Bound pending work and provider concurrency, isolate a failing tool dependency, and return explicit admission or deadline outcomes instead of creating unlimited tasks.

**Implementation todo:** Use bounded .NET channels, cancellation/deadlines, and separate concurrency limits for independent resources. Surface overload rejection and queue latency. Start in one process; only claim distributed/global limits when using shared coordination.

**Acceptance:** With teaching settings of queue capacity 8 and provider concurrency 2, measured pending work never exceeds 8 and in-flight provider calls never exceed 2. Saturation is reported; cancelled queued jobs do not invoke the model. A blocked tool bulkhead does not occupy another tool's independently reserved capacity.

**Boundary:** Bounded channels are not durable queues. No claim that local limits enforce a multi-replica quota.

**Sources:** Independent extension of the reports' resource-control theme using S10 and S11.

### B11 - EvalOps and Controlled Rollout

**Problem and novelty:** Chapter 19 implements evaluation, not automatic release governance. The missing contract compares versioned agent trajectories and controls which candidate may act.

**Demo:** Compare baseline and candidate versions on synthetic held-out tasks, replay recorded failures, run the candidate in no-write shadow mode, then exercise canary assignment and rollback through a local traffic harness.

**Implementation todo:** Reuse B09 fixtures and adapters. Version prompt/model/tool/skill configurations and datasets. Require all deterministic policy checks to pass; define quality, cost, and latency thresholds before execution. Use repeated trials and report unavailable metrics explicitly.

**Acceptance:** A policy regression blocks promotion even if answer-quality scores improve. Shadow execution cannot mutate systems. Canary assignment is stable per synthetic tenant; a rollback sends subsequent requests to the baseline. Existing candidate in-flight work is identified and handled explicitly rather than assumed undone.

**Boundary:** Local simulation is not a production traffic rollout. A real traffic plane, cloud evaluation jobs, and Azure changes are separate, explicitly authorized work.

**Sources:** Enterprise report section 13; Chapter 19 guide; S8.

## Suggested next steps

1. **Choose one first extension:** B01 for the action-safety path, B03 for durable execution, or B04 for a self-contained capability-packaging demo. They are independent entry points, not a mandatory three-project batch.
2. **Continue by demonstrated need:** B02 after B01; B08 after B01 and an explicit sandbox-runtime decision; B11 after B09. B05, B06, B07, B09, and B10 can begin independently.
3. **Implement one extension at a time:** assign the next unused `ExtensionNN.PatternName` only when implementing it. Do not reserve chapter numbers or scaffold eleven empty projects.

## Definition of done for each future extension

- Explain the pattern, when to use/not use it, novelty relative to existing demos, tradeoffs, combinations, and sources in its README.
- Provide one reusable C# runner and a meaningful caller-supplied scenario, not only fixed sample output. Keep teaching policies and synthetic data explicit.
- Follow [shared build settings](../dotnet/Directory.Build.props), [central package management](../dotnet/Directory.Packages.props), shared configuration, naming, and solution registration. The inspected baseline uses .NET 10 and MAF 1.22.0, with several preview companion packages.
- Check current documentation against pinned source/package APIs before choosing dependencies. Do not treat illustrative snippets in the research reports as compile-ready contracts or silently upgrade the entire solution.
- Add focused deterministic tests for both successful behavior and the pattern's defining failure boundary. Use controlled clocks/fakes where appropriate, but do not substitute mocks for a required real crash-recovery or isolation demonstration.
- Keep live inference/cloud checks opt-in and credential-free tests in normal CI. Add suitable smoke coverage through the existing protected default-branch workflow only when warranted.
- Add a hosted companion only if it preserves semantics. Durable workers, sandboxes, and release pipelines may need a different companion host or no Foundry Agent Service wrapper.
- Make authentication, shared state, retention, resource ownership, costs, and cleanup explicit. No deployment, resource creation, or role assignment is implied by implementing local code.

## Deliberately deferred

- **Generic Magentic/manager-led orchestration:** overlaps Chapter 07's coordinator and bounded-loop teaching; revisit only with a distinct task/progress-ledger contract.
- **Generic SLM/LLM routing:** overlaps Chapter 16. A future local-first data-residency demo must prove that restricted data never reaches cloud inference, not just choose a smaller alias.
- **Full computer-use/coding agents:** broad applications that compose many patterns, not a small next extension.
- **Portable memory interchange, universal skill signing/attestation, and cross-vendor registries:** track standards, but do not present unsettled interoperability as a supported contract.
- **Another generic reflection/evaluator/budget/RAG demo:** already covered unless a future proposal identifies a new missing behavioral guarantee.

## Sources and evidence limits

| ID | Primary source | Contribution and limit |
|---|---|---|
| S1 | [Microsoft Learn: Durable Extension](https://learn.microsoft.com/en-us/agent-framework/hosting/durable-extension) | C# support, persistent sessions/workflows, external events, and Azure Functions versus bring-your-own-compute hosting. Documentation includes prerelease installation; compatibility with MAF 1.22.0 needs implementation-time verification. |
| S2 | [Anthropic: Effective context engineering for AI agents](https://www.anthropic.com/research/effective-context-engineering-for-ai-agents) | Just-in-time retrieval, context selection, compaction, and long-horizon context strategies. Engineering guidance, not a quantitative promise for this repository. |
| S3 | [Azure Architecture Center: Compensating Transaction](https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction) | Business-specific compensation, persisted recovery progress, idempotent commands, and manual intervention. |
| S4 | [Agent Skills specification](https://agentskills.io/specification) | Package format and progressive disclosure. The format does not itself enforce the proposed approval, provenance, or capability policy. |
| S5 | [Microsoft Learn: Agent Skills, C#](https://learn.microsoft.com/en-us/agent-framework/agents/skills?pivots=programming-language-csharp) | Runtime providers, sources, and loading. Application runtime functionality, not the repository's coding-assistant skill. |
| S6 | [MCP authorization specification, 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization) | HTTP authorization and resource/scope boundaries. Not a complete application-specific permission system. |
| S7 | [Anthropic: Claude Code sandboxing](https://www.anthropic.com/research/claude-code-sandboxing) | Filesystem/network isolation and credentials outside the sandbox. Product-specific evidence; no blanket claim that any sandbox prevents every escape. |
| S8 | [Anthropic: Demystifying evals for AI agents](https://www.anthropic.com/research/demystifying-evals-for-ai-agents) | Tasks, repeated trials, transcripts, outcomes, and layered graders. Supports evaluating state changes rather than trusting final prose. |
| S9 | [Azure Architecture Center: Event Sourcing](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing) | Append-only records, rehydration, versioning, and adoption costs. Inspiration for narrow recording/replay, not a repository-wide event-sourcing requirement. |
| S10 | [Azure Architecture Center: Queue-Based Load Leveling](https://learn.microsoft.com/en-us/azure/architecture/patterns/queue-based-load-leveling) | Buffering, bounded downstream rates, queue durability, and overload tradeoffs. |
| S11 | [.NET Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) | Bounded producer/consumer primitives for a runnable single-process backpressure demonstration. |

These sources were retrieved directly. Priority and proposed demonstrations are this backlog's synthesis; sources do not establish benchmark superiority or universal production readiness.
