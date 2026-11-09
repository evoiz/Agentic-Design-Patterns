# Update Conference: local C# demos

The six demos are presented in this order: **1. Prague weather**, **2. Simple MAF workflow**, **3. Schedule RAG with TextSearchProvider**, **4. Planning and goal monitoring**, **5. Decision Model**, and **6. Harness**. These are focused examples, not one integrated agenda product. All conference-specific code lives here; existing chapter implementations stay unchanged.

## Setup

Use the .NET 10 SDK policy in the repository's [global.json](../global.json). Package versions come from [the central .NET manifest](../dotnet/Directory.Packages.props), imported rather than copied: MAF/Workflows/Harness 1.22.0 and Decisions.Foundry 0.7.0. The host also uses the repository's pinned preview Foundry hosting package.

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
| **5.1 Decision Model - Microsoft live** | Six live Microsoft decision calls |
| **5.2 Decision Model - live comparison** | Microsoft then OpenAI for each question |
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

The **5.1 Decision Model - Microsoft live** profile calls Microsoft only. **5.2 Decision Model - live comparison** calls both configured decision deployments sequentially for each question. Profiles contain no credentials.

Model-backed demos print their user question or request before inference. The planning demo prints its SMART goals first, and both decision commands identify the complete schedule evidence and shared attendee goal without dumping the document. Decision questions, choices, and the score scale are shown before their respective calls.

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
    05-DecisionModels/              DecisionDemo.cs (5.1-5.2)
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

Getting started showed linear edges. This workflow combines **Planning + Reflection + Goal Monitoring + Parallelization**, with routing as the supporting control mechanism. A task planner interprets a brief and produces an actionable strategy; a separate builder executes that strategy to produce an agenda. The task plan is how to do the work, not the agenda itself. Review and feedback drive replanning until all goals pass or the budget is spent.

```powershell
dotnet run --project ConferenceDemos -- plan              # live run (max 3 rounds)
dotnet run --project ConferenceDemos -- plan --rounds 1   # may escalate if goals remain unmet
dotnet run --project ConferenceDemos -- plan --verbose   # full task instructions, checks and final rationales
```

The [demo 4 flow diagram](demo-flows.md#demo-4-planning-and-goal-monitoring) documents the graph in [AgendaPlanningDemo.cs](ConferenceDemos/Demos/04-PlanningAndGoalMonitoring/AgendaPlanningDemo.cs); the CLI runs the workflow without printing Mermaid.

| Executor | Pattern | Role |
|---|---|---|
| `PlanTasks` | Planning | Interprets the current brief and returns 2-6 actionable tasks with expected outcomes; prints the acceptance contract for the audience without injecting it into the initial model input |
| `BuildAgenda` | Execution | A separate `AgendaBuilder` consumes the task plan and a flat day schedule with actual times, returning a structured `PlannedAgenda` (ids + why) |
| `CheckConstraints` ‖ `CritiqueFit` | Parallelization (`AddFanOutEdge`) | Deterministic code checks the eight hard goals while `AgendaCritic` reviews all acceptance goals and interest fit, scoring overall quality 1-5 and suggesting repairs (reflection). The critic uses the same inexpensive checker for authoritative evidence rather than guessing hard-goal results |
| `MonitorGoals` | Fan-in barrier + goal monitoring | `AddFanInBarrierEdge` waits for both checks, prints a PASS/FAIL goal table, and routes |
| conditional edges | Routing | `goals unmet: revise` loops back to task planning with concrete evidence and revealed policies; `all goals met` → `PublishAgenda`; `budget exhausted` → `Escalate`, reporting the need for human intervention |

The initial brief asks for **four interesting sessions** on conference day 1, with interests, own talk #13, and coverage of at least two AI sessions besides the own talk and one .NET session. The monitor additionally requires no overlaps and **12:30 free for speaker preparation**. These two policies are deliberately absent from initial planner/builder instructions, but are disclosed to the audience. The schedule still supplies accurate times. A lightweight brief may produce overlapping choices or a booked preparation slot; a model can also infer sensible scheduling and pass immediately.

On rejection, the monitor reveals concrete conflicts or preparation bookings. Subsequent rounds retain all revealed requirements, generate a new task plan, and build a fresh agenda. The critic knows the full acceptance contract from round 1, including the preparation window and overlap rules, and receives the day schedule to suggest feasible repairs. Its score covers overall agenda quality, not just topic relevance: instructions require a score of at most 3 when a hard goal fails and actionable feedback naming every violation. Repair advice reaches the planner and builder even if the critic's score passes. Model judgment can still be wrong; deterministic hard-goal checks remain authoritative. This is refinement from incomplete requirements, not proof that the planner ignored instructions. The fixed C# outer graph remains a bounded planner-to-worker workflow, not an autonomous task scheduler.

The console streams `AgendaProgressEvent` narration so the audience sees task plans, drafts, parallel checks, goal results, and routes. Iteration history retains each task plan. A `ModelCallBudgetClient` caps inference at **three calls per round**. Parallelization demonstrates independent review branches, not a guaranteed speedup. `--rounds 1` escalates only when goals fail; it cannot force rejection of a valid agenda. A successful run exits 0, escalation exits 2, and errors exit 1. Escalation reports unmet goals but does not implement interactive human approval.

Default output is presentation-focused: numbered short task titles, agendas with room/track details on separate lines, grouped passing checks, explicit failure evidence, a separate wrapped critic paragraph, and the next route. Full task actions and expected outcomes still reach the builder and remain in iteration history. `--verbose` adds those details, the full acceptance contract, individual passing checks, and final session rationales. Text wraps at 96 columns without truncating evidence; default output avoids repeating the final agenda that was just shown.
### 5. Decision Model: live Microsoft and OpenAI comparison

Configure the Microsoft decision deployment alias from the repository root. Add the OpenAI alias for the live comparison:

```powershell
dotnet user-secrets set "Foundry:ModelDeployments:decision" "<Microsoft-Decision-1-deployment>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployments:openaiDecision" "<Azure-OpenAI-Decisions-deployment>" --project dotnet/src/Shared
```

Or use `Foundry__ModelDeployments__decision` and `Foundry__ModelDeployments__openaiDecision`. Deploy the selected decision models in advance; an ordinary chat deployment is not interchangeable. This demo reuses the chapter's provider configuration and the pinned Decisions client, rather than imitating a decision model with chat prompts.

From this folder, run Microsoft live (six API calls):

```powershell
dotnet run --project ConferenceDemos -- decisions
```

For demo **5.2**, compare both providers live (twelve API calls):

```powershell
dotnet run --project ConferenceDemos -- decisions --compare-openai
```

For each question, Microsoft is called first and its result printed, then OpenAI is called with the exact same state, question, options or rubric. Both results appear in the same question block before moving to the next task. Both clients are configured before inference begins. All displayed results come from the APIs; there is no recording, fixture, replay, or file-path argument.

Each question has a numbered block: question, options or rubric, shared-state summary, then provider name, API method and **RUNNING** status, **COMPLETED**, and a compact result. Noul runs first (both days), then Choice, then three separate Score calls. Probabilities are readable percentages; scores remain zero-based expected levels. Raw JSON and usage metadata are omitted. Presentation formatting lives in [DecisionPresentation.cs](ConferenceDemos/Demos/05-DecisionModels/DecisionPresentation.cs), leaving `EvaluateAsync` focused on `AssessAsync`, `ChooseAsync`, and `ScoreAsync`.

[DecisionDemo.cs](ConferenceDemos/Demos/05-DecisionModels/DecisionDemo.cs) supplies the **complete schedule, both days**, to every call. `AttendeeGoal` and `ScoreRubric` are editable in that file. Keep the rubric aligned with the interests you want to assess; editing a goal changes the input to both live providers immediately.

| Shape | Library method | Displayed result |
|---|---|---|
| Noul (binary), two calls | `AssessAsync` | "Is Cédric Mendelin speaking on Day 1?" and "Day 2?"; separate proposition probabilities |
| Choice | `ChooseAsync` | One session ID and probabilities across eleven speaker/session options |
| Ordered score, three calls | `ScoreAsync` | Fit of non-AI sessions #6 (garbage collection), #18 (HttpClient), and #20 (advanced TypeScript); expected rubric level and four level probabilities |

The shortlist is sessions **#5, #9, #13, #17, #21, #22, #30, #32, #34, #43, and #46**, with titles and speaker labels taken from the schedule. Cédric's talk participates on equal terms: no winner is hard-coded. David's adoption talk and Kajetan's implementation talk illustrate a genuine trade-off; the other options cover broader agent development, operations, security, and AI-assisted work. This selects a talk, not a ranking of people or a conflict-free agenda.

The score rubric is **0: no useful coverage**, **1: adjacent coverage**, **2: direct coverage of one need**, **3: direct coverage of both integration and colleague adoption**. The three non-AI topics provide negative or adjacent-fit examples against the agent integration/adoption goal: runtime knowledge or networking may be useful background, but should not automatically earn direct-fit scores. Judge only the abstract, not what a speaker might discuss. No exact live scores are prescribed. The expected score is not a percentage or a ranking of the shortlist.

The snapshot lists Cédric as a named speaker on Day 1, not Day 2. Generic speaker Q&A participation is not inferred. These factual questions illustrate the binary API; production schedule lookup should remain deterministic. Exact live probabilities and the recommendation winner are not prescribed.

These are normalized typed outputs, not identical raw provider wire formats. The Microsoft adapter uses SystemOne; the OpenAI adapter uses the Decisions endpoint. Both providers' binary probabilities, choice distributions, and expected-score consistency are validated.

Missing configuration, provider errors, cancellation, and malformed distributions fail explicitly; there is no fallback result. Full context is not silently truncated; ensure the chosen deployments accept the schedule. This is an API/output illustration, **not an accuracy benchmark**; neither scores nor probabilities are calibrated real-world certainty.

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

Run demos 1–3 first, then demos 4–6 in order after configuring the real deployments. Rehearse both live decision providers and follow the hosted guide through portal invocation. The decision demo always uses live inference and fails explicitly if a dependency fails. Keep the 50-minute content / 10-minute Q&A budget from the [talk outline](conference-talk-outline.md); automated tests cannot establish a timed rehearsal or portal success.

Tests use the repository's scripted `IChatClient` and Responses host fixtures, not credentials. They exercise actual MAF function calling, workflow events, the context provider, workflows/Harness, and the hosted protocol with controlled model replies. Schedule tests check complete-document injection, caller/day propagation, plain-text output, and explicit failures; live rehearsal is still needed to assess answer quality. The shared code comes from `dotnet/src/Shared`, `Shared.Hosting`, and linked chapter decision-configuration/call-budget source files. Conference prompts, the agenda workflow, schedule grounding, the page skill, and the Harness setup remain here.

The [original implementation plan](ticket-demo-implementation-plan.md) tracks the later demos and presenter-only gates. Only the schedule demo has a deployment companion; deploying it does not deploy the getting-started weather tool or the other demos.
