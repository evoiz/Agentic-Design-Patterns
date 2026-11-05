# Update Conference Talk Demo Implementation Plan

> Planning document only. The demos described here have not been implemented or deployed.

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

## Suggested code shape

Add a clearly named conference-demo area under `dotnet/src/` with one local console entry point and explicit commands for the four demos, plus a focused test project. Keep the schedule Hosted Agent companion separate so local demos do not inherit hosting/deployment requirements. Reuse repository patterns where appropriate, but do not turn this into a shared framework or modify existing chapter behavior just to fit the talk.

Use the existing [schedule Markdown file](Update_Conference_Prague_2026_Schedule.md) as the canonical event fixture. Package or embed it for local execution and the Hosted Agent deployment; add a check that the deployed package contains the schedule. Avoid maintaining a second manually edited copy.

## Risk-first implementation sequence

### 0. Confirm presentation prerequisites

- Select the Foundry project and model deployment for rehearsal; confirm region/model availability and the presenter's authorized access.
- Confirm that local inference succeeds with the chosen .NET configuration before building dependent demos.
- Pin the versions of MAF, Harness, the Decisions package, and the .NET SDK used by the samples. Verify current APIs during implementation rather than copying assumptions from old snippets.
- Keep secrets in local user-secrets/environment configuration. Never commit credentials or depend on them in deterministic tests.
- Decide the presentation speaker only after finding a suitable official or speaker-authored source for their profile and next announced talk. If none can be verified, use a scenario that explicitly reports that no future talk was verified.

### 1. Build the schedule-backed RAG demo first

- Add a local MAF agent that uses the Markdown schedule through a small, read-only retrieval function.
- Keep retrieval inspectable and proportionate to this small corpus; a vector database is not a prerequisite. Preserve session title, time, and source reference so the final answer can be grounded.
- Accept the user's interest and request as input. The first run should answer the basic "What should I attend today if I'm interested in AI and .NET?" question; the flow/review prompts in the talk are motivation cards, not promises of extra finished features.
- Run from a local .NET process using the selected Foundry inference deployment. Print only the final answer, with concise source references if supported; do not print retrieved passages or internal tool output.
- Test that relevant schedule entries can be retrieved, the response does not invent an unavailable session, and source references point to the canonical fixture. Add a no-match case that returns an explicit limitation instead of a success-shaped guess.

**Acceptance gate:** a presenter can run the demo from a clean checkout after configuring credentials, ask the basic question, and get a schedule-grounded final response without exposing retrieval output.

### 2. Add and rehearse the Hosted Agent companion

- Host the custom C# agent behavior as a Microsoft Foundry Hosted Agent; do not substitute a Prompt Agent or describe the local console as deployed.
- Use the Azure Developer CLI (`azd`) deployment path for the prepared project. Keep the Foundry project, model deployment, and hosting prerequisites ready before the talk; do not provision resources live.
- Ensure the Markdown fixture is included in the Hosted Agent package and that the hosted implementation uses the same retrieval/answer behavior as the local demo where practical.
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
- Run deterministic tests for parsing/retrieval, workflow goal checks, all three decision shapes, harness/tool limits, revision limits, and citation validation.
- Separately smoke-test live inference for the local demos and the portal invocation for the Hosted Agent. Keep these checks separate from deterministic tests.
- Prepare captures for each live dependency, especially Hosted Agent deployment/invocation, Microsoft-Decision-1, and speaker research.
- Enforce the 50-minute content budget and preserve the 10-minute Q&A. If behind, shorten explanation or switch to captures; do not add features or cut the portal invocation acceptance check from pre-talk rehearsal.

## Demo contracts and on-stage boundaries

| Demo | Data and tools | Visible result | Explicit non-goals |
|---|---|---|---|
| Schedule RAG | Canonical schedule Markdown; local read-only retrieval | Final schedule-grounded answer only | No vector-store deployment, no printed retrieval trace, no broad production-search claim |
| Shopping planner | Fixed recipes, preferences, pantry; local fake/read-only functions | Missing-items shopping list and concise goal-check status | No hosted app, live recipe lookup, route, or budget |
| Decision Models | One session abstract and attendee interest; one live provider and one capture | Choice, Noul, and ordered score examples | No accuracy benchmark; no model for exact schedule facts |
| Speaker collaboration | Pre-verified local source snapshot; read-only researcher tools | Cited summary, bounded review, at most one revision | No live web search, unverified future-event claims, inferred "passion," or reviewer tools |

## Completion checklist

- [ ] Four local .NET commands are documented and runnable with configured local credentials.
- [ ] The conference schedule is the single source fixture and is packaged for local and Hosted Agent execution.
- [ ] The local schedule answer prints no retrieved passages.
- [ ] The Hosted Agent is deployed and invoked successfully in the portal after model-inference access is verified.
- [ ] Shopping goal checks and failure cases are covered by deterministic tests.
- [ ] Decision Models covers choice, Noul, and score with the selected live/captured provider arrangement.
- [ ] Speaker claims/citations are evidence-checked; tool and revision limits are enforced.
- [ ] Captures/fallbacks and a 50-minute timed rehearsal are ready.
