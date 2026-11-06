# Update Conference demo flows

Use the matching numbered profile in
[launchSettings.json](ConferenceDemos/Properties/launchSettings.json).
Run the commands below from `update-conference-26`.
See the [demo guide](README.md#demos-in-presentation-order) for configuration.

| Demo | Launch profile | Command | Flow |
|---|---|---|---|
| 2 | `2. Simple MAF workflow` | `dotnet run --project ConferenceDemos -- workflow Alex` | [Simple MAF workflow](#demo-2-simple-maf-workflow) |
| 4 | `4. Planning and goal monitoring` | `dotnet run --project ConferenceDemos -- plan` | [Planning and goal monitoring](#demo-4-planning-and-goal-monitoring) |
| 6 | `6. Harness` | `dotnet run --project ConferenceDemos -- speaker-page --open` | [Harness agent](#demos-6-and-61-harness-agent) |
| 6.1 | `6.1 Harness + web search` | `dotnet run --project ConferenceDemos -- speaker-page --web --open` | [Harness agent with optional web search](#demos-6-and-61-harness-agent) |

## Demo 2: Simple MAF workflow

[WelcomeWorkflowDemo.cs](ConferenceDemos/Demos/02-SimpleMafWorkflow/WelcomeWorkflowDemo.cs) uses three
delegate executors. This is a deterministic workflow, not an LLM agent:
the attendee profile is constructed in code and the recommendation is selected
by an `AI` substring check.

```mermaid
flowchart TD
  Attendee["Attendee name (default: Alex via CLI)"];
  GetAttendeeProfile["GetAttendeeProfile: name + .NET / AI interests"];
  RecommendSession["RecommendSession: select session from profile"];
  CreateRecommendation["CreateRecommendation: format recommendation"];
  Output["Print executor results and completion message"];

  Attendee --> GetAttendeeProfile;
  GetAttendeeProfile --> RecommendSession;
  RecommendSession --> CreateRecommendation;
  CreateRecommendation --> Output;
```

## Demo 4: Planning and goal monitoring

[AgendaPlanningDemo.cs](ConferenceDemos/Demos/04-PlanningAndGoalMonitoring/AgendaPlanningDemo.cs) owns the workflow
edges below. The one-round launch profile uses this same graph with a smaller
budget; it escalates only if the goals are still unmet.

```mermaid
flowchart TD
  ReadGoals["ReadGoals (Start)"];
  PlanAgenda["PlanAgenda"];
  CheckConstraints["CheckConstraints"];
  CritiqueFit["CritiqueFit"];
  MonitorGoals["MonitorGoals"];
  PublishAgenda["PublishAgenda"];
  Escalate["Escalate"];

  fan_in_MonitorGoals_1D9BB972((fan-in))
  CheckConstraints --> fan_in_MonitorGoals_1D9BB972;
  CritiqueFit --> fan_in_MonitorGoals_1D9BB972;
  fan_in_MonitorGoals_1D9BB972 --> MonitorGoals;
  ReadGoals -->|SMART goals| PlanAgenda;
  PlanAgenda -->|draft agenda| CheckConstraints;
  PlanAgenda -->|draft agenda| CritiqueFit;
  MonitorGoals -. goals unmet: revise .-> PlanAgenda;
  MonitorGoals -. all goals met .-> PublishAgenda;
  MonitorGoals -. budget exhausted .-> Escalate;
```

## Demos 6 and 6.1: Harness agent

[SpeakerPageDemo.cs](ConferenceDemos/Demos/06-Harness/SpeakerPageDemo.cs) configures one
`HarnessAgent`, not a fixed `WorkflowBuilder` graph. This diagram shows its
tool-selection loop and the code-owned final verification. The model chooses
tool order and may repeat calls; the skill guides it to check and repair the page.

Demo **6** supplies the GitHub username and disables web search. Demo **6.1**
omits the username from the prompt and enables hosted web search to find it
and further-reading links. Both variants download the photo over HTTP.

```mermaid
flowchart TD
  Start["Load speaker profile; prepare output folder"];
  Harness["HarnessAgent: choose next action"];
  Skills["Read speaker-agenda-page skill and design guide"];
  Schedule["ListSessions / GetSession"];
  Web["6.1 only: hosted web search for GitHub username and reading links"];
  Avatar["DownloadGitHubAvatar"];
  Files["Sandboxed file tools: read / write agenda.html"];
  Check["CheckPage: deterministic page checks"];
  Feedback["Return failures so agent can repair page"];
  Finish["Agent run ends"];
  Verify["Code runs Verify independently"];
  Passed{"All final checks pass?"};
  Ready["Report page ready; exit 0"];
  Incomplete["Report failed checks; exit 1"];

  Start --> Harness;
  Harness --> Skills --> Harness;
  Harness --> Schedule --> Harness;
  Harness -. web enabled .-> Web --> Harness;
  Harness --> Avatar --> Harness;
  Harness --> Files --> Harness;
  Harness --> Check;
  Check -->|checks fail| Feedback --> Harness;
  Check -->|checks pass| Harness;
  Harness -->|final response| Finish --> Verify --> Passed;
  Passed -->|yes| Ready;
  Passed -->|no| Incomplete;
```

With `--open`, the CLI opens an existing page after reporting the checks, even
when checks fail. Exceptions or cancellation follow the CLI's error path
rather than reporting success.
