# Chapter 21 — Hosted Exploration & Discovery

Expose the [bounded proposal-and-review pattern](../Chapter21.ExplorationDiscovery/README.md) through Foundry Responses. Long-running Foundry Deep Research remains a separate console variant, not a silently changed hosted capability.

## Where to look in code

[ExplorationHostedAgent.cs](ExplorationHostedAgent.cs), `RunAsync`: thin shared-adapter wrapper. [ExplorationWorkflow.RunAsync](../Chapter21.ExplorationDiscovery/ExplorationWorkflow.cs): budgets, parallel reviewer panel, acceptance/revision. [RequestIsolationKeyProvider.GetKeysAsync](RequestIsolationKeyProvider.cs): response-ID isolation even without a user header.

## Request contract

The latest nonempty user text supplies a research topic of at most **4,000 characters**. Fresh request state produces one text report containing a structured proposal, evidence needs, risks, and three independent reviewer assessments.

- At most **two rounds / eight model calls total**: one explorer plus three reviewers in each round.
- A linked **three-minute** cancellation budget covers the whole proposal/review operation.
- Every reviewer must explicitly report its ethics flag, return Accept with an overall score ≥ 7, and have **no ethical concern** before code accepts.
- Otherwise revise once; exhausted rounds return “needs human review,” not false acceptance.
- Invalid proposals/reviews fail explicitly. Caller cancellation propagates; internal time-budget expiry becomes a timeout.

No browsing, experiments, or code execution is claimed. Output is an unverified proposal, not a literature review or experimental finding. Response-ID-scoped isolation and fresh agents prevent application history reuse. Responses streaming buffers one complete reviewed report.

## Local commands

Configure [shared model settings](../../../README.md), then:

```powershell
dotnet run --project dotnet\src\02-ReasonAndImprove\Chapter21.ExplorationDiscovery.Hosted
```

In another terminal, from this project's directory:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter21-exploration-discovery --local --protocol responses "Explore whether batching reduces inference cost without harming tail latency."
```

## Deploy and operate

Complete [HOSTING.md](../../../HOSTING.md): reader-owned project/model selection, managed identity/inference RBAC, bundled dependencies, sensitive trace handling, costs, and scoped cleanup. Local execution supports developer credentials or user-secrets; keep API keys out of hosted Azure configuration. After setup, from this project's directory:

```powershell
azd deploy chapter21-exploration-discovery --no-prompt
azd ai agent show chapter21-exploration-discovery --output json
# Verify runtime inference access before invoking.
azd ai agent invoke chapter21-exploration-discovery --protocol responses "Explore adaptive batching and tail latency."
```

Agreement among reviewers is not independent evidence. Keep human review for consequential research decisions. Hosting Deep Research would require an explicit durable-job, persistence, and polling design rather than removing its domain/tool/time safeguards.
