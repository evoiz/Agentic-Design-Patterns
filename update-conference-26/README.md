# Update Conference: local C# demos

Four separate demonstrations, not one integrated agenda product. All conference-specific code lives here; existing chapter implementations stay unchanged.

## Setup

Use the .NET 10 SDK selected by [global.json](global.json). Package versions come from [the central .NET manifest](../dotnet/Directory.Packages.props), imported rather than copied: MAF/Workflows/Harness 1.22.0 and Decisions.Foundry 0.7.0. The host also uses the repository's pinned preview Foundry hosting package.

From the repository root, configure the [shared local credentials](../dotnet/README.md) once:

```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "https://<account>.services.ai.azure.com/api/projects/<project>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ApiKey" "<your-key>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<chat-deployment>" --project dotnet/src/Shared
```

Use a chat model supporting tools and structured JSON output. Equivalent environment variables are `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, and `Foundry__ModelDeployment`. Keep keys out of code, shell recordings, captures, and Git. Local means code, tools, and fixtures run on your machine; model inference still calls Foundry.

Then work **from this folder**:

```powershell
Set-Location update-conference-26
dotnet build ConferenceDemos.slnx
dotnet run --project ConferenceDemos -- check-data
dotnet test ConferenceDemos.Tests/ConferenceDemos.Tests.csproj
```

`check-data` and the tests need no credentials or network model calls. The canonical [schedule](Update_Conference_Prague_2026_Schedule.md) is an embedded resource, not a second edited copy. Source IDs such as `Update_Conference_Prague_2026_Schedule.md#s13` resolve to its anchors.

### Launch profiles

[ConferenceDemos/Properties/launchSettings.json](ConferenceDemos/Properties/launchSettings.json) provides IDE launch profiles for all four demos, the plain schedule baseline, day 2/no-match retrieval, shopping failure cases, OpenAI recording, and the offline data check. Select a profile in your IDE, or run one by name from this folder:

```powershell
dotnet run --project ConferenceDemos --launch-profile "Schedule - RAG day 1"
dotnet run --project ConferenceDemos --launch-profile "Check data"
```

The **Decisions - compare** profile uses the bundled illustrative JSON sample, so no recording is needed. **Decisions - record OpenAI** and **Decisions - compare recorded OpenAI** use `captures\openai-decisions.json`, relative to the process working directory; use the same working directory for recording and comparison. Recording requires the configured OpenAI decision deployment and refuses to overwrite an existing capture. Profiles contain no credentials.

Each demo prints its user question or request before inference. Shopping also shows the pantry input, and both decision commands show the shared session and attendee interest. Decision questions, choices, and the score scale are supplied directly at their respective method calls.

## 1. Schedule RAG: same question, different evidence

```powershell
dotnet run --project ConferenceDemos -- plain "What should I attend today if I'm interested in AI and .NET?"
dotnet run --project ConferenceDemos -- rag "What should I attend today if I'm interested in AI and .NET?"
dotnet run --project ConferenceDemos -- rag --day 2 "AI and .NET"
dotnet run --project ConferenceDemos -- rag "unicorn astrophysics"
```

`plain` has no schedule and must disclose that limitation. `rag` retrieves from the bundled snapshot using deterministic token matching, exposes the matching entries through one small read-only tool, and asks MAF for structured session recommendations. Code checks session IDs and exact evidence quotes, then supplies titles, times, rooms, and citations from the fixture. The console prints the user question followed by the final answer, not tool traces or the retrieved JSON.

Day 1 is November 12 and day 2 is November 13, both CET. **"Today" defaults to conference day 1**, not the computer's date; choose day 2 explicitly. There is no vector database or web search. A no-match result states the limitation; unsupported IDs/quotes fail instead of becoming recommendations. Retrieval relevance is illustrative, not semantic-search quality. This does not promise a non-overlapping agenda.

Look at [ScheduleCorpus.cs](ConferenceDemos/ScheduleCorpus.cs) and [ScheduleDemo.cs](ConferenceDemos/ScheduleDemo.cs). The [separate Hosted Agent guide](ScheduleAgent.Hosted/README.md) covers running and deploying exactly this behavior.

## 2. Shopping: planning with a code-owned goal check

```powershell
dotnet run --project ConferenceDemos -- shopping vegetarian
dotnet run --project ConferenceDemos -- shopping vegan
dotnet run --project ConferenceDemos -- shopping keto
dotnet run --project ConferenceDemos -- shopping vegetarian --incomplete
```

[ShoppingDemo.cs](ConferenceDemos/ShoppingDemo.cs) makes the flow visible: `ChooseMeal -> ReadPantry -> CheckGoal`. The model chooses only among recipes that satisfy the explicit preference. The pantry is a local read-only dictionary: `true` means present, `false` means missing, and absent/null means unknown. Code, not a second model, calculates missing ingredients and checks exact ingredient accounting.

A passing run prints the meal, missing items only, and `Goal check PASS`. Unsupported preferences, unknown pantry data, or invalid choices print `FAIL`, return exit code 1, and never show a completed shopping list. `keto` intentionally demonstrates no matching recipe. No pricing, budget, route, recipe service, or deployment is involved.

## 3. Decision Models: live Microsoft, sample or captured OpenAI

Configure the Microsoft decision deployment alias from the repository root. Add the OpenAI alias only if recording real OpenAI output:

```powershell
dotnet user-secrets set "Foundry:ModelDeployments:decision" "<Microsoft-Decision-1-deployment>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployments:openaiDecision" "<Azure-OpenAI-Decisions-deployment>" --project dotnet/src/Shared
```

Or use `Foundry__ModelDeployments__decision` and `Foundry__ModelDeployments__openaiDecision`. Deploy the selected decision models in advance; an ordinary chat deployment is not interchangeable. This demo reuses the chapter's provider configuration and the pinned Decisions client, rather than imitating a decision model with chat prompts.

From this folder, run with the bundled sample (only the Microsoft deployment is called live):

```powershell
dotnet run --project ConferenceDemos -- decisions
```

The checked-in [openai-decisions.sample.json](ConferenceDemos/Samples/openai-decisions.sample.json) contains all three output shapes in one file. It is copied to build and publish output and resolved relative to the executable, not the working directory. Its values are **illustrative, not recorded model output**, and the console explicitly labels them that way. `check-data` also validates this file offline.

**Why JSON?** Only to replay the comparison output without making a second set of live model calls. Neither provider requires a JSON file for inference. The sample removes the recording prerequisite; a real capture is useful for presenting actual OpenAI behavior.

To replace the illustration with **real OpenAI output**, record it before the talk:

```powershell
dotnet run --project ConferenceDemos -- decisions-record-openai captures\openai-decisions.json
dotnet run --project ConferenceDemos -- decisions captures\openai-decisions.json
```

The recording command makes three live OpenAI Decisions calls, validates the responses, and creates a new file without overwriting an existing capture. Review it before presenting. Real captures are ignored by Git; the illustrative sample is committed. With an explicit capture path, the presentation command validates the file first, then calls **Microsoft-Decision-1 live** for all three shapes and displays **Azure OpenAI Decisions - CAPTURED**, with model and capture timestamp. A missing explicit path fails; it never silently falls back to the sample.

[DecisionDemo.cs](ConferenceDemos/DecisionDemo.cs) uses the same session #13 abstract and stated AI/C#/Foundry attendee interest for:

| Shape | Library method | Displayed result |
|---|---|---|
| Choice | `ChooseAsync` | Relevance label and label probabilities |
| Noul (binary) | `AssessAsync` | `AssessmentDecision.Probability` |
| Ordered score | `ScoreAsync` | Zero-based expected score and level probabilities on a four-level scale |

These are the library's normalized typed outputs, not identical raw provider wire formats. The Microsoft adapter uses SystemOne; the OpenAI adapter uses the Decisions endpoint. Only the session and attendee interest are shared; each question and its options or scale are inline in `EvaluateAsync`. JSON includes this shared input, all three results, and a task-contract version. Real captures also include provider/model and timestamp; the sample deliberately has no model or timestamp. Input equality, contract version, provenance fields, and result distributions are checked, but do not prove provider origin: create real captures only with the recording command. Older version-1 captures must be re-recorded for this version-2 format.

Missing/mismatched captures and malformed distributions fail explicitly. This is an API/output illustration, **not an accuracy benchmark**; neither model scores nor probabilities are calibrated real-world certainty. Schedule existence and time lookup stay deterministic.

## 4. Speaker collaboration: bounded research and review

```powershell
dotnet run --project ConferenceDemos -- research
dotnet run --project ConferenceDemos -- research "Which public topics in Shay Rojansky's supplied abstract fit my AI and .NET interests?"
```

[SpeakerDemo.cs](ConferenceDemos/SpeakerDemo.cs) derives its source pack from the canonical organizer snapshot for Shay Rojansky's session #9. The pack carries the exact session URL, retrieval date, and excerpt. It is not a separately verified profile or future-event announcement; the output explicitly says **no next talk was verified**. Requests are restricted to this evidence scope, not open-ended research about arbitrary speakers.

The MAF Harness wraps only the Researcher, with one read-only source tool, no web/file/skills tools, a shared eight-model-call budget across initial draft and revision, a four-source-call budget, and a three-minute deadline. `WorkflowBuilder` controls `Research -> Review -> Finalize`. The Reviewer gets the draft, excerpts, and deterministic errors but has no tools and at most one model call. Code allows **zero or one revision**, then verifies every quote/citation again; invalid final evidence fails. The final output shows review feedback, revision/call counts, cited public-topic quotes, and the explicit future-event limitation, not hidden reasoning.

Quotes are deliberately extractive so exact claims are checkable; this is not a general semantic fact checker. A reviewer may approve immediately, so a live run is not guaranteed to demonstrate revision. Deterministic tests exercise rejection, one revision, invalid revision, source/tool bounds, and citation failures.

## Rehearsal and boundaries

Run the four demos in talk order after configuring the real deployments. Separately record the OpenAI capture, rehearse live inference, and follow the hosted guide through portal invocation. Prepare captures of live-dependent demos and switch to them if a dependency fails. Keep the 50-minute content / 10-minute Q&A budget from the [talk outline](conference-talk-outline.md); automated tests cannot establish a timed rehearsal or portal success.

Tests use the repository's scripted `IChatClient` and Responses host fixtures, not credentials. They exercise actual MAF workflows/Harness and the hosted protocol with controlled model replies. The shared code comes from `dotnet/src/Shared`, `Shared.Hosting`, and linked chapter decision-configuration/call-budget source files. Conference prompts, recipe flow, schedule retrieval, and collaboration remain here.

Implementation sequencing and outstanding presenter-only gates are tracked in the [implementation plan](ticket-demo-implementation-plan.md). Only the schedule demo has a deployment companion; deploying it does not deploy the other three demos.
