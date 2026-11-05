# Update Conference Talk Demo Implementation Plan

> Local implementations, deterministic checks, and the separate Hosted Agent companion are implemented. Live model rehearsal, real provider captures, and Azure/portal validation remain external acceptance gates; no deployment has been performed.

## Current progress

The implementation location is `update-conference-26/`, as requested, rather than a new area under `dotnet/src/`. Existing chapter demos remain unchanged.

| Step | Status | Persisted work / remaining gate |
|---|---|---|
| 0. Prerequisites | Code setup complete; live setup pending | Projects import existing .NET build/package settings, reference Shared, and select the .NET 10 SDK feature band. No local shared credentials are configured; presenter project/model selection and live inference remain. |
| 1. Schedule RAG | Full-document grounding | Canonical Markdown is embedded; the schedule agent receives the complete document through MAF's built-in `TextSearchProvider` before ordinary text generation. Session parsing remains for other demos. Relevance and citations are model-generated rather than exact-quote validated. |
| 2. Hosted Agent | Shared schedule behavior | Separate Responses host, managed-identity configuration, tracing, bundled azd manifest, and project-local deployment/provenance guide exist. Publish must include the complete embedded fixture and referenced dependencies. The host delegates to the same full-document schedule agent. |
| 3. Shopping workflow | Implemented and deterministically checked | `WorkflowBuilder` controls recipe choice, read-only pantry accounting, and goal validation. Tests cover supported preference, no matching recipe, absent/null pantry data, extra/duplicate/missing items, and model failure. |
| 4. Decision Models | Implemented and deterministically checked | Choice, Noul, score, provider configuration, and real-capture recording/playback are wired. Tests verify shared context, distribution contracts, capture metadata/hash, stale inputs, cancellation, and failures. No fabricated capture is included; live provider calls and real recording remain. |
| 5. Speaker collaboration | Implemented and deterministically checked | Real Harness researcher, tool-free reviewer, one-revision workflow, exact quote/citation checks, and call limits are exercised with scripted models. Organizer snapshot only; no next event is claimed to be verified. Live rehearsal remains. |
| 6. Validation and rehearsal | Separate automated and presenter gates | Build the conference solution, run deterministic tests, and check published dependencies/fixture loading. Rehearse live answers separately from scripted checks; portal invocation, real provider captures, and timed rehearsal remain presenter gates. |

### Completed implementation sequence

1. Restored and compiled local demos, correcting executor constructors, typed workflow input dispatch, and canonical fixture parsing.
2. Added focused deterministic tests using the repository's scripted clients and real MAF WorkflowBuilder/Harness APIs.
3. Added `ScheduleAgent.Hosted/` using the shared stateless adapter and official Foundry Responses package; checked published fixture/dependencies, protocol output, and request isolation.
4. Added the [local run guide](README.md) and [project-local deployment guide](ScheduleAgent.Hosted/README.md), including exact creation provenance, identity/RBAC, portal acceptance, telemetry, and safe cleanup.
5. Verified the conference solution, tests, and published host. Existing chapter implementations remain untouched.

### Remaining presenter rehearsal gates

1. Configure the chosen Foundry chat and decision deployments and rehearse all four local commands with live inference.
2. Record and review a real Azure OpenAI Decisions capture using the documented command; rehearse the Microsoft-Decision-1 live / OpenAI captured comparison.
3. Follow the hosted guide from its project folder, deploy with authorization, verify current runtime inference access, and obtain a grounded portal response.
4. Prepare captures/fallbacks and rehearse the timed 50-minute content path with 10 minutes reserved for Q&A.

Current environment constraints: Azure CLI and `azd` were not found during discovery; online documentation/source retrieval failed DNS resolution, and the Foundry skill could not be loaded. Existing pinned repository samples are the reference until those capabilities are available. No Azure resource or permission changes have been made.

## Goal and scope

Build a coherent sequence of small, locally runnable .NET demonstrations that supports the talk outline. The schedule RAG example is the only demo with an optional Foundry Hosted Agent deployment and portal invocation.

**Local** means the .NET application and its tools/data run on the presenter's machine. Live model calls may use the preconfigured Foundry deployment. No Blazor app, Azure SQL database, App Service, live web search, or onstage infrastructure provisioning is a conference-critical prerequisite.

Keep existing chapter samples intact unless a specific reusable helper is clearly appropriate. The talk-specific shopping workflow, Decision Models example, speaker-research collaboration, and schedule-backed RAG variant must be described as new examples until they are built and verified.

## Demo backlog

| Order | Demo | Runtime | Talk purpose |
|---:|---|---|---|
| 1 | Update schedule RAG | Local .NET app; optional custom Hosted Agent | Same question with and without event data; answer from the Markdown schedule. |
| 2 | Shopping planner with goal check | Local .NET app | Use `WorkflowBuilder` to choose a simple dinner and return only missing ingredients. |
| 3 | Decision Models | Local .NET app | Demonstrate choice, Noul, and score; Microsoft-Decision-1 live, Azure OpenAI Decisions captured. |
| 4 | Speaker research with Harness and Reviewer | Local .NET app | Bounded collaboration over a verified source snapshot, with one evidence-focused revision. |

## Implementation layout

All conference-specific code lives directly under `update-conference-26/`: `ConferenceDemos/`, `ConferenceDemos.Tests/`, and separate `ScheduleAgent.Hosted/`, grouped in `ConferenceDemos.slnx`. Build/package settings import from `dotnet/`; existing Shared projects and small chapter helpers are reused. The local console does not reference the host or require deployment tooling.

Use the existing [schedule Markdown file](Update_Conference_Prague_2026_Schedule.md) as the canonical event fixture. Package or embed it for local execution and the Hosted Agent deployment; add a check that the deployed package contains the schedule. Avoid maintaining a second manually edited copy.

## Risk-first implementation sequence

### 0. Confirm presentation prerequisites

- Select the Foundry project and model deployment for rehearsal; confirm region/model availability and the presenter's authorized access.
- Confirm that local inference succeeds with the chosen .NET configuration before building dependent demos.
- Pin the versions of MAF, Harness, the Decisions package, and the .NET SDK used by the samples. Verify current APIs during implementation rather than copying assumptions from old snippets.
- Keep secrets in local user-secrets/environment configuration. Never commit credentials or depend on them in deterministic tests.
- Decide the presentation speaker only after finding a suitable official or speaker-authored source for their profile and next announced talk. If none can be verified, use a scenario that explicitly reports that no future talk was verified.

### 1. Build the schedule-backed RAG demo first

- Add a local MAF agent that uses the complete Markdown schedule through the built-in `TextSearchProvider` in `BeforeAIInvoke` mode.
- Supply one full-document context result without keyword ranking, chunking, or a model-driven retrieval tool. A vector database is not a prerequisite; allow enough model context for the document.
- Accept the user's interest and request as input. The first run should answer the basic "What should I attend today if I'm interested in AI and .NET?" question; the flow/review prompts in the talk are motivation cards, not promises of extra finished features.
- Run from a local .NET process using the selected Foundry inference deployment. Print only the final answer, with concise source references if supported; do not print retrieved passages or internal tool output.
- Test complete-document injection, caller/day propagation, plain-text output, cancellation, input limits, and explicit failures. Instruct the model to cite schedule anchors and disclose absent evidence; assess factuality and day/relevance selection during live rehearsal rather than claiming deterministic answer validation.

**Acceptance gate:** a presenter can run the demo from a clean checkout after configuring credentials, ask the basic question, and get a schedule-grounded final response without exposing retrieval output.

### 2. Add and rehearse the Hosted Agent companion

- Host the custom C# agent behavior as a Microsoft Foundry Hosted Agent; do not substitute a Prompt Agent or describe the local console as deployed.
- Use the Azure Developer CLI (`azd`) deployment path for the prepared project. Keep the Foundry project, model deployment, and hosting prerequisites ready before the talk; do not provision resources live.
- Ensure the Markdown fixture is included in the Hosted Agent package and that the hosted implementation uses the same context-provider/answer behavior as the local demo.
- Resolve the deployed runtime identity and verify its model-inference permission at the model-hosting resource scope during setup. Recheck effective access after the final deployment rehearsal. Do not create or change role assignments onstage.
- Invoke the deployed agent from the Foundry portal after deployment. A successful code deployment alone is not the acceptance test.
- Prepare a short capture of deployment and a verified portal invocation. During the talk, cap the live deployment/invocation at the outline's allotted time; switch to the capture and a previously deployed agent if it runs long or fails.

**Acceptance gate:** the prepared deployment can be invoked in the portal and returns a grounded schedule answer; runtime inference access and fixture packaging have been checked after the final deployment.

### 3. Build the local Planning and Monitoring demo

- Use a small, fixed recipe set, explicit preference, and explicit pantry inventory.
- Use MAF `WorkflowBuilder` to choose a fitting meal, check pantry contents with local fake/read-only tools, and produce only the missing shopping items.
- Add a deterministic goal check: the meal satisfies the stated preference and every required ingredient is accounted for as either already in the pantry or on the shopping list. Show a concise pass/fail status with the list.
- Do not add a web UI, live recipe service, route, price, budget, or unrelated planning requirement.
- Test the supported path, a preference with no matching recipe, and incomplete pantry data. A failed goal check must be visible and must not be presented as a completed shopping list.

**Acceptance gate:** one command runs the complete local workflow and shows the shopping list plus the goal-check result. The demo remains understandable without explaining every workflow API.

### 4. Build the Decision Models example

- Use one schedule-session abstract and an explicitly stated attendee interest as shared context.
- Exercise all three task shapes with the same context: **choice**, **Noul** (binary yes/no), and **score** on an ordered scale.
- Call **Microsoft-Decision-1 live** from the local .NET demo, as selected for the talk.
- Capture a previously verified **Azure OpenAI Decisions** response for the same inputs and task shapes. Clearly label it as captured; do not imply it is a second live call.
- Describe the comparison as an API/output illustration. Do not compare numerical outputs as a benchmark, claim either provider is more accurate, or present a model score as calibrated certainty.
- Keep deterministic schedule lookups outside the model. The live decision is about bounded relevance/fit, not discovering whether a session exists or when it occurs.

**Acceptance gate:** a rehearsal can show all three live Microsoft-Decision-1 task shapes, then the labeled Azure OpenAI Decisions capture, with matching input/context and no unsupported performance claim.

### 5. Build the bounded speaker-research collaboration

- Select a real schedule speaker only when a future talk can be verified from official or speaker-authored sources. Snapshot the source excerpts, exact URLs, and retrieval date in a local fixture; no live web search is used during the talk.
- Use a local MAF Researcher agent with read-only tools over that fixture. Compose it with `AsHarnessAgent(HarnessAgentOptions)` and explicit execution/tool limits.
- Use a separate Reviewer that receives the Researcher's draft and cited evidence but has no search or action tools. Keep handoff and revision count under code-owned `WorkflowBuilder` control.
- Allow at most one revision. Validate that final claims map to supplied evidence and that citations resolve to the fixture. The answer must say when no future event can be verified.
- Phrase findings as publicly discussed topics; do not infer a person's private beliefs, motivation, or "passion."
- Use a deterministic scripted model client for tests and a live local inference path for rehearsal. Prepare a safe capture in case the live response is not suitable for the allotted time.

**Acceptance gate:** the local demo completes a bounded researcher-reviewer handoff, demonstrates at most one revision, and produces only evidence-backed claims with valid citations (or explicitly reports that a future talk was not verified).

### 6. Rehearse the full talk path

- Test each local command independently from a clean checkout; then rehearse transitions, prompts, and output framing in the exact talk order.
- Run deterministic tests for schedule parsing/context injection, workflow goal checks, all three decision shapes, harness/tool limits, revision limits, and speaker citation validation.
- Separately smoke-test live inference for the local demos and the portal invocation for the Hosted Agent. Keep these checks separate from deterministic tests.
- Prepare captures for each live dependency, especially Hosted Agent deployment/invocation, Microsoft-Decision-1, and speaker research.
- Enforce the 50-minute content budget and preserve the 10-minute Q&A. If behind, shorten explanation or switch to captures; do not add features or cut the portal invocation acceptance check from pre-talk rehearsal.

## Demo contracts and on-stage boundaries

| Demo | Data and tools | Visible result | Explicit non-goals |
|---|---|---|---|
| Schedule RAG | Complete canonical Markdown via MAF `TextSearchProvider` | Final plain-text answer with model-generated citations | No vector-store deployment, printed context, deterministic factuality guarantee, or broad production-search claim |
| Shopping planner | Fixed recipes, preferences, pantry; local fake/read-only functions | Missing-items shopping list and concise goal-check status | No hosted app, live recipe lookup, route, or budget |
| Decision Models | One session abstract and attendee interest; one live provider and one capture | Choice, Noul, and ordered score examples | No accuracy benchmark; no model for exact schedule facts |
| Speaker collaboration | Pre-verified local source snapshot; read-only researcher tools | Cited summary, bounded review, at most one revision | No live web search, unverified future-event claims, inferred "passion," or reviewer tools |

## Completion checklist

- [x] Four local .NET commands are implemented and documented for configured local credentials (live rehearsal remains).
- [x] The conference schedule is the single source fixture and is packaged for local and Hosted Agent execution.
- [x] The local schedule answer prints only final text, not the injected schedule; relevance and citations are model-generated.
- [x] The separate host and project-local step-by-step deployment/creation guide are present.
- [ ] The Hosted Agent is deployed and invoked successfully in the portal after model-inference access is verified.
- [x] Shopping goal checks and failure cases are covered by deterministic tests.
- [x] Decision Models implements choice, Noul, and score with the selected live/captured provider arrangement.
- [ ] Live Microsoft decisions and a real matching OpenAI capture are rehearsed.
- [x] Speaker claims/citations are evidence-checked; tool and revision limits are enforced.
- [ ] Captures/fallbacks and a 50-minute timed rehearsal are ready.
