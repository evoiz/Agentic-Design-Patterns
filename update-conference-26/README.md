# Update Conference: local C# demos

The six demos are presented in this order: **1. Prague weather**, **2. Simple MAF workflow**, **3. Schedule RAG with TextSearchProvider**, **4. Planning and goal monitoring**, **5. Decision Model**, and **6. Harness**. These are focused examples, not one integrated agenda product. All conference-specific code lives here; existing chapter implementations stay unchanged.

## Setup

Use the .NET 10 SDK selected by [global.json](global.json). Package versions come from [the central .NET manifest](../dotnet/Directory.Packages.props), imported rather than copied: MAF/Workflows/Harness 1.22.0 and Decisions.Foundry 0.7.0. The host also uses the repository's pinned preview Foundry hosting package.

From the repository root, configure the [shared local credentials](../dotnet/README.md) once:

```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "https://<account>.services.ai.azure.com/api/projects/<project>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ApiKey" "<your-key>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<chat-deployment>" --project dotnet/src/Shared
```

Use a chat model supporting function/tool calling for getting-started step 1, structured JSON output for the planning demo, and tool calling for the Harness page demo; `speaker-page --web` also needs a deployment that supports hosted web search through the Responses API. The schedule demo needs only text generation with enough context for the complete schedule (about 75 KB). Step 2 needs no model or credentials. Equivalent environment variables are `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, and `Foundry__ModelDeployment`. Keep keys out of code, shell recordings, captures, and Git. Local means code, tools, and fixtures run on your machine; model inference still calls Foundry.

Then work **from this folder**:

```powershell
Set-Location update-conference-26
dotnet build ConferenceDemos.slnx
dotnet test ConferenceDemos.Tests\ConferenceDemos.Tests.csproj
```

The tests need no credentials or network model calls. The canonical [schedule](Update_Conference_Prague_2026_Schedule.md) is an embedded resource, not a second edited copy. Source IDs such as `Update_Conference_Prague_2026_Schedule.md#s13` resolve to its anchors.

### Launch profiles

[ConferenceDemos/Properties/launchSettings.json](ConferenceDemos/Properties/launchSettings.json) lists the demos in presentation order:

| Profile | Purpose |
|---|---|
| **1. Prague weather** | Agent + weather tool |
| **2. Simple MAF workflow** | Deterministic session recommendation |
| **3. Schedule RAG (TextSearchProvider)** | Schedule-grounded answers |
| **4. Planning and goal monitoring** | Plan, check, and revise an agenda |
| **5.1 Decision Model - compare sample** | Live Microsoft output and illustrative OpenAI sample |
| **5.2 Decision Model - compare recorded OpenAI** | Live Microsoft output and recorded OpenAI output |
| **5.3 Decision Model - record OpenAI** | Prepare a real OpenAI capture |
| **6. Harness** | Build a speaker page without web search |
| **6.1 Harness + web search** | Build a speaker page with web search |

The supplemental **Planning - one round (escalation path)** profile limits the
budget to one round; escalation occurs only if goals remain unmet.
The [flow diagrams](demo-flows.md) cover demos **2**, **4**, **6**, and **6.1**.
Select a profile in your IDE, or run one by name from this folder:

```powershell
dotnet run --project ConferenceDemos --launch-profile "3. Schedule RAG (TextSearchProvider)"
dotnet run --project ConferenceDemos --launch-profile "6.1 Harness + web search"
```

The **5.1 Decision Model - compare sample** profile uses the bundled illustrative JSON sample, so no recording is needed. **5.3 Decision Model - record OpenAI** and **5.2 Decision Model - compare recorded OpenAI** use `captures\openai-decisions.json`, relative to the process working directory; use the same working directory for recording and comparison. Recording requires the configured OpenAI decision deployment and refuses to overwrite an existing capture. Profiles contain no credentials.

Model-backed demos print their user question or request before inference. The planning demo prints its SMART goals first, and both decision commands show the shared session and attendee interest. Decision questions, choices, and the score scale are supplied directly at their respective method calls.

## Demos in presentation order

### Code layout

The source folders under `ConferenceDemos` follow the demo numbers:

```text
ConferenceDemos/
  Program.cs
  Demos/
    01-PragueWeather/               AgentDemo.cs
    02-SimpleMafWorkflow/           WelcomeWorkflowDemo.cs
    03-ScheduleRag/                 ScheduleWorkflowDemo.cs, ScheduleDemo.cs
    04-PlanningAndGoalMonitoring/   AgendaPlanningDemo.cs
    05-DecisionModels/              DecisionDemo.cs (5.1-5.3)
    06-Harness/                     SpeakerPageDemo.cs (6 and 6.1)
  Shared/                          ConferenceWorkflow.cs, ScheduleCorpus.cs
  Samples/
  Skills/
```

`Program.cs` dispatches the commands. Decision and Harness variants share their
respective implementation rather than duplicating code. Samples and skills stay
at their existing paths so runtime content loading remains unchanged.

Run these first three commands **from this folder**, in order:

```powershell
dotnet run --project ConferenceDemos -- agent
dotnet run --project ConferenceDemos -- workflow Alex
dotnet run --project ConferenceDemos -- rag
```

### 1. Prague weather: agent + tool

[AgentDemo.cs](ConferenceDemos/Demos/01-PragueWeather/AgentDemo.cs) creates one `ChatClientAgent` from the shared `IChatClient`, gives it short instructions, and calls `RunAsync` twice:

1. "In one sentence, welcome a .NET developer to Update Conference in Prague."
2. "What is the weather like in Prague today?"

The first answer is ordinary model-generated text. For the second, MAF lets the model invoke `GetPragueWeather`, a normal C# method exposed with `[Description]` and `AIFunctionFactory.Create`. The tool prints its invocation and returns **fixed demo data, not a live forecast**. No weather service, location parser, or additional configuration is needed.

Expect two question/answer pairs, with `Tool call: GetPragueWeather -> Fixed demo data, not a live forecast: Prague is cloudy, 6 C.` during the weather interaction. The exact model wording can vary. The tool result is data the model uses to compose its answer; it is not another agent.

**Concepts:** `IChatClient` connects to the model; an agent adds instructions and tools; `RunAsync` runs an interaction; a tool lets the model request application code.

### 2. Simple MAF workflow: recommend a session

[WelcomeWorkflowDemo.cs](ConferenceDemos/Demos/02-SimpleMafWorkflow/WelcomeWorkflowDemo.cs) builds `GetAttendeeProfile -> RecommendSession -> CreateRecommendation`. Three delegate executors construct a .NET/AI attendee profile, select a session with a string check, and format the recommendation. This demo makes **no model calls** and works without Foundry credentials. See the [demo 2 flow](demo-flows.md#demo-2-simple-maf-workflow).

```powershell
dotnet run --project ConferenceDemos -- workflow Alex
```

```text
2. Simple MAF workflow (no model calls)
GetAttendeeProfile -> RecommendSession -> CreateRecommendation

GetAttendeeProfile: Alex: .NET Developer interested in AI
RecommendSession: Building Agentic AI with Microsoft Foundry
CreateRecommendation: Recommended session: Building Agentic AI with Microsoft Foundry
Completed workflow.
```

**Concepts:** `BindAsExecutor` turns a delegate into a workflow step; `AddEdge` passes its result to the next executor; `WithOutputFrom` selects the final output. The demo runs the graph with `InProcessExecution.RunAsync` and prints each executor's result from completion events.

Omitting the CLI name uses `Alex`. A workflow controls application execution and does not inherently need an agent or an LLM.

### 3. Schedule RAG with TextSearchProvider: put an agent inside a workflow

Ask "What should I attend today if I'm interested in AI and .NET?" with schedule grounding:

```powershell
dotnet run --project ConferenceDemos -- rag "What should I attend today if I'm interested in AI and .NET?"
```

`rag` runs the explicit workflow in [ScheduleWorkflowDemo.cs](ConferenceDemos/Demos/03-ScheduleRag/ScheduleWorkflowDemo.cs): **PrepareQuestion -> AnswerFromSchedule**. The first executor validates and passes along the question; the second calls the existing grounded agent. The console shows the question, executor sequence, executor-completion labels, and one final answer, never the injected document.

Inside that second executor, [ScheduleDemo.cs](ConferenceDemos/Demos/03-ScheduleRag/ScheduleDemo.cs) uses MAF's built-in `TextSearchProvider` in `BeforeAIInvoke` mode to supply the **complete embedded schedule Markdown** as one context result before the model runs. There is no keyword ranking, chunking, retrieval tool loop, or structured JSON response. The agent makes one model call and returns a normal answer with session titles, times, rooms, and `Update_Conference_Prague_2026_Schedule.md#s...` citations.

**Concepts:** a workflow executor can call an agent; a context provider supplies knowledge before inference; grounding and citations connect the answer to conference evidence. Unlike step 1's model-selected weather tool, this provider supplies context automatically.

Day 1 is November 12 and day 2 is November 13, both CET. **"Today" defaults to conference day 1**, not the computer's date; choose day 2 explicitly. Both days are supplied; instructions select the requested day. The model is instructed to disclose absent evidence instead of inventing details. Relevance, day selection, and citations are model-generated, not deterministically checked. This is full-document grounding through MAF's RAG context-provider mechanism, not semantic/vector search or a conflict-free agenda planner. It trades more input tokens for a simpler demo without retrieval-recall failures.

Look at [ScheduleWorkflowDemo.cs](ConferenceDemos/Demos/03-ScheduleRag/ScheduleWorkflowDemo.cs), [ScheduleDemo.cs](ConferenceDemos/Demos/03-ScheduleRag/ScheduleDemo.cs), and [ScheduleCorpus.cs](ConferenceDemos/Shared/ScheduleCorpus.cs). The [separate Hosted Agent guide](ScheduleAgent.Hosted/README.md) still runs and deploys the same reusable grounded agent; the new console workflow does not change its hosting contract.

After demo 3, **an agent chooses a tool, a workflow controls steps, and a grounded agent can be one of those steps**. The examples follow the official [add-tools guide](https://learn.microsoft.com/en-us/agent-framework/get-started/add-tools?pivots=programming-language-csharp) and [first-workflow guide](https://learn.microsoft.com/en-us/agent-framework/get-started/workflows?pivots=programming-language-csharp), using this repository's pinned SDK and shared configuration.

### 4. Planning and goal monitoring: a self-correcting agenda workflow

Getting started showed linear edges. This workflow combines **planning, parallelization, reflection, routing, and goal monitoring** in one graph, in the spirit of the Chapter 11 `GoalSettingWorkflow` loop: set SMART goals, plan, monitor, and revise until the goals are met or the budget is spent.

```powershell
dotnet run --project ConferenceDemos -- plan              # live run (max 3 rounds)
dotnet run --project ConferenceDemos -- plan --rounds 1   # may escalate if goals remain unmet
```

The [demo 4 flow diagram](demo-flows.md#demo-4-planning-and-goal-monitoring) documents the graph in [AgendaPlanningDemo.cs](ConferenceDemos/Demos/04-PlanningAndGoalMonitoring/AgendaPlanningDemo.cs); the CLI runs the workflow without printing Mermaid.

| Executor | Pattern | Role |
|---|---|---|
| `ReadGoals` | Goal setting | Prints nine SMART goals for Cédric's day 1: exactly 5 day-1 sessions, no overlaps, must attend #13, keep 12:30 free for talk prep, at least 2 AI and 1 .NET session, and a fit score of at least 4/5 |
| `PlanAgenda` | Planning | `AgendaPlanner` agent returns a structured `PlannedAgenda` (ids + why) from the day's time slots, the goals, and the previous round's feedback |
| `CheckConstraints` ‖ `CritiqueFit` | Parallelization (`AddFanOutEdge`) | Deterministic code checks the eight hard goals while an `AgendaCritic` agent scores fit 1-5 (reflection) |
| `MonitorGoals` | Fan-in barrier + goal monitoring | `AddFanInBarrierEdge` waits for both checks, prints a PASS/FAIL goal table, and routes |
| conditional edges | Routing | `goals unmet: revise` loops back with concrete feedback (for example `#1/#2 both at 09:15 (keep one)`); `all goals met` → `PublishAgenda`; `budget exhausted` → `Escalate` to a human with the best draft |

The console streams the run (`RunStreamingAsync`) through a custom `AgendaProgressEvent`, so the audience sees each draft, the two parallel checks, the goal table, and the chosen route. Code, not the model, decides whether the hard goals are met; the critic only owns the subjective fit goal. A `ModelCallBudgetClient` caps the run at two model calls per round. Live models often satisfy all goals in round 1. In rehearsal, round 1 also produced an overlap plus a booked 12:30 slot and needed revision, so both paths are realistic. `--rounds 1` makes escalation reachable on demand. A successful run exits 0, an escalation exits 2, and failures exit 1.
### 5. Decision Model: live Microsoft, sample or captured OpenAI

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

The checked-in [openai-decisions.sample.json](ConferenceDemos/Samples/openai-decisions.sample.json) contains all three output shapes in one file. It is copied to build and publish output and resolved relative to the executable, not the working directory. Its values are **illustrative, not recorded model output**, and the console explicitly labels them that way. The decision tests validate this file offline.

**Why JSON?** Only to replay the comparison output without making a second set of live model calls. Neither provider requires a JSON file for inference. The sample removes the recording prerequisite; a real capture is useful for presenting actual OpenAI behavior.

Demo **5.1** uses the sample. To replace it with **real OpenAI output**, record it with **5.3** before running the comparison in **5.2**:

```powershell
dotnet run --project ConferenceDemos -- decisions-record-openai captures\openai-decisions.json
dotnet run --project ConferenceDemos -- decisions captures\openai-decisions.json
```

The recording command makes three live OpenAI Decisions calls, validates the responses, and creates a new file without overwriting an existing capture. Review it before presenting. Real captures are ignored by Git; the illustrative sample is committed. With an explicit capture path, the presentation command validates the file first, then calls **Microsoft-Decision-1 live** for all three shapes and displays **Azure OpenAI Decisions - CAPTURED**, with model and capture timestamp. A missing explicit path fails; it never silently falls back to the sample.

[DecisionDemo.cs](ConferenceDemos/Demos/05-DecisionModels/DecisionDemo.cs) uses the same session #13 abstract and stated AI/C#/Foundry attendee interest for:

| Shape | Library method | Displayed result |
|---|---|---|
| Choice | `ChooseAsync` | Relevance label and label probabilities |
| Noul (binary) | `AssessAsync` | `AssessmentDecision.Probability` |
| Ordered score | `ScoreAsync` | Zero-based expected score and level probabilities on a four-level scale |

These are the library's normalized typed outputs, not identical raw provider wire formats. The Microsoft adapter uses SystemOne; the OpenAI adapter uses the Decisions endpoint. Only the session and attendee interest are shared; each question and its options or scale are inline in `EvaluateAsync`. JSON includes this shared input, all three results, and a task-contract version. Real captures also include provider/model and timestamp; the sample deliberately has no model or timestamp. Input equality, contract version, provenance fields, and result distributions are checked, but do not prove provider origin: create real captures only with the recording command. Older version-1 captures must be re-recorded for this version-2 format.

Missing/mismatched captures and malformed distributions fail explicitly. This is an API/output illustration, **not an accuracy benchmark**; neither model scores nor probabilities are calibrated real-world certainty. Schedule existence and time lookup stay deterministic.

### 6. Harness: build my speaker agenda page (HTML)

One MAF `HarnessAgent` turns the schedule into a personal web page: Cédric's photo, his own talk, a day timeline of fitting sessions, and (with `--web`, demo **6.1**) further reading found by live web search. The layout may differ between runs; the console ends with a short success summary. See the [demo 6 / 6.1 flow](demo-flows.md#demos-6-and-61-harness-agent) for the tool loop and final verification.

```powershell
dotnet run --project ConferenceDemos -- speaker-page --open        # out\speaker-page\agenda.html
dotnet run --project ConferenceDemos -- speaker-page --web --open  # + hosted web search
```

The whole agent is one options object in [SpeakerPageDemo.cs](ConferenceDemos/Demos/06-Harness/SpeakerPageDemo.cs). The harness already brings the agent loop, todos, skills, file tools, and web search; the demo only adds four small tools, a skill, and a sandbox folder:

```csharp
client.AsHarnessAgent(new HarnessAgentOptions
{
    Name = "SpeakerPageBuilder",
    ChatOptions = new ChatOptions { Tools = [ListSessions, GetSession, DownloadGitHubAvatar, CheckPage] }, // AIFunctions, shortened
    AgentSkillsSource = new AgentFileSkillsSource(SkillsDirectory),  // how to build the page
    FileAccessStore = new FileSystemAgentFileStore(outputDirectory), // the only folder it can write to
    DisableWebSearch = !webSearch,                                   // hosted web_search tool, run by Foundry
    DisableAgentModeProvider = true                                  // just execute; no plan-then-approve stop
});
```

The two approval settings in the file auto-approve the read-only skill tools and the sandboxed file tools; without them the harness pauses for human approval. `AllowMultipleToolCalls = false` keeps one tool call per model turn.

A typical tool trace: `load_skill` → `GetSession(13)` → `ListSessions(1)` → `GetSession` per pick → `web_search` (with `--web`) → `DownloadGitHubAvatar` → `read_skill_resource(design.md)` → `file_access_write(agenda.html)` → `CheckPage`. When `CheckPage` reports a failure (for example an overlap or the own talk missing from the timeline), the agent fixes the file and calls it again. The [speaker-agenda-page skill](ConferenceDemos/Skills/speaker-agenda-page/SKILL.md) holds the steps, rules, and sections; its [design guide](ConferenceDemos/Skills/speaker-agenda-page/references/design.md) is loaded only when the agent writes the page.

**How web search works.** `DisableWebSearch = false` adds the hosted `web_search` tool to the request. It is not a local tool: the Foundry (Azure OpenAI) Responses API runs the search server-side and returns text with URL citations to the model, so the demo uses the Responses client. Web search returns text, not files, so it cannot download images. With `--web` the profile is sent without the GitHub handle: the agent searches for the username, then calls `DownloadGitHubAvatar(username)`. That tool builds `https://github.com/<username>.png` in code (the model never supplies a URL, so it cannot invent someone else's picture), accepts only image responses, and saves `speaker.png` or `speaker.jpg` next to `agenda.html`. Without `--web` the prompt contains the username. The image stays a separate file instead of base64 inside the page, because the model would otherwise have to write thousands of base64 characters.

The speaker profile is [Samples/speaker-profile.json](ConferenceDemos/Samples/speaker-profile.json). The same code checks back the agent's `CheckPage` tool and, after the run, print the success summary: page saved, speaker name, photo, own talk on the timeline, only real schedule sessions, no overlapping sessions, and web links when `--web` is set. A failing check exits 1.

**Demo only, not for production:** generated HTML is not sanitized, and `--open` opens an existing page even when checks fail. The checks validate agenda content, not HTML safety; the skill's no-script instruction is not an enforced boundary. Model-generated content, including content influenced by web results, can execute in your browser outside the file-tool sandbox. Inspect the file before opening it. Production use requires an enforced HTML safety boundary and a safe preview policy.
## Rehearsal and boundaries

Run demos 1–3 first, then demos 4–6 in order after configuring the real deployments. Separately record the OpenAI capture, rehearse live inference, and follow the hosted guide through portal invocation. Prepare captures of live-dependent demos and switch to them if a dependency fails. Keep the 50-minute content / 10-minute Q&A budget from the [talk outline](conference-talk-outline.md); automated tests cannot establish a timed rehearsal or portal success.

Tests use the repository's scripted `IChatClient` and Responses host fixtures, not credentials. They exercise actual MAF function calling, workflow events, the context provider, workflows/Harness, and the hosted protocol with controlled model replies. Schedule tests check complete-document injection, caller/day propagation, plain-text output, and explicit failures; live rehearsal is still needed to assess answer quality. The shared code comes from `dotnet/src/Shared`, `Shared.Hosting`, and linked chapter decision-configuration/call-budget source files. Conference prompts, the agenda workflow, schedule grounding, the page skill, and the Harness setup remain here.

The [original implementation plan](ticket-demo-implementation-plan.md) tracks the later demos and presenter-only gates. Only the schedule demo has a deployment companion; deploying it does not deploy the getting-started weather tool or the other demos.
