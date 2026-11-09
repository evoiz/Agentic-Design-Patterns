# Agentic AI in C#: From a Useful Answer to a Useful System

## Talk direction

**Audience:** .NET developers curious about agentic AI.  
**Slot:** 60 minutes total: 50 minutes of talk and demos, followed by 10 minutes of Q&A.  
**Thesis:** Start with the task, then add only the structure needed to close a demonstrated gap. Models provide judgment; application code controls evidence, tools, boundaries, and execution.

The motivation starts with one relatable question about the Update Conference schedule. The C# introduction then follows a three-step getting-started path: arrive in Prague with an agent and weather tool, learn workflow mechanics through a code-only attendee welcome, and put the grounded schedule agent inside a workflow. The audience then sees separate, practical C# examples for a goal-monitored planning workflow, decision models, and an Agent Harness that builds a verified HTML artifact. The closing returns to the schedule assistant as a conceptual composition; it must not imply that all of these demonstrations already form one integrated product.

## Motivation: one question, growing needs

Use the same visual language throughout: simple prompt cards, short answer excerpts, and small context labels. Do not introduce pattern diagrams in the motivation. Use real entries from the [conference schedule](Update_Conference_Prague_2026_Schedule.md); verify any displayed session titles and times against that file.

| Beat | User request / context | What the audience should notice |
|---|---|---|
| Plain LLM | "What should I attend today if I'm interested in AI and .NET?" Context label: **No current schedule**. | A general model does not know this event's current agenda. Do not stage a fabricated confident answer as a fact. |
| Add knowledge (RAG) | Repeat the exact same question. Context label: **Schedule available**. | The answer can now name relevant sessions, but retrieval alone does not guarantee a conflict-free, well-formed personal agenda. If showing a conflict, use an actual parallel slot from the schedule. |
| Add a flow | "Build me a three-session agenda today focused on AI and .NET. Use only schedule entries, explain why each fits, include the times, and avoid overlaps." | The user now needs a sequence of constrained steps, not just relevant snippets. |
| Add review | "Check that agenda against my interests, the schedule evidence, and the no-overlap requirement. Correct unsupported choices or conflicts." | A result needs an explicit check against criteria. Keep the criteria visible; do not imply that a fluent answer is proof of correctness. |
| Need a harness | "Turn my checked agenda into a page I can share: my photo, my talk, the sessions I picked, why they fit, and a few current links on agentic AI." | One agent now needs a plan, a page contract, file access, schedule tools, optional web search, and limits. A harness packages those capabilities; code still verifies the artifact. |

End the motivation with a brief, non-prescriptive spectrum: **answer -> grounded answer -> planned and checked work -> a bounded agent that produces an artifact**. It is not a maturity ladder or a claim that every task needs an agent. Transition with: **"How do we build only the structure we need in C#, and where does that code run?"**

## Timing

| Segment | Slides | Time |
|---|---:|---:|
| Intro and motivation | 1-4 | 5 min |
| C# Loves AI: SDK roles and three-step MAF introduction | 5-6 | 5 min |
| Foundry basics, deployment, and portal invocation | 7-8 | 7 min |
| Agentic Design Patterns overview | 9 | 2 min |
| Planning and Monitoring | 10-11 | 6 min |
| Decision Models | 12-13 | 10 min |
| Agent Harness: speaker agenda page | 14-16 | 11 min |
| Composition, resources, and close | 17 | 4 min |
| **Talk and demos** | **1-17** | **50 min** |
| Q&A | 18 | 10 min |
| **Total** |  | **60 min** |

## Slide-by-slide story and visuals

### 1. Opening (0:30)

- Introduce the connection to classic software design patterns: reusable ways to organize recurring problems, not recipes to apply everywhere.
- Set expectations: a motivating example, the C# / Foundry building blocks, three focused pattern demonstrations, and practical resources.

### 2. Same question, no schedule versus schedule (1:30)

- Place two copies of the exact same user prompt side by side.
- Label them **No current schedule** and **Schedule available**; show only a short, verified final-answer excerpt for the latter.
- Explain the first limitation plainly: a model cannot reliably answer an event-specific question without current event data.
- For the RAG answer, show only the final answer, not retrieved passages or internal tool output. If it names sessions, keep citations concise.

### 3. From relevant sessions to a usable agenda (1:30)

- Show the three-session, conflict-free request as a prompt card.
- Contrast it with the prior question: the user now specifies constraints and desired structure. Show a real schedule overlap only if verified (for example #1 and #2 both run at 09:15 on day 1).
- Use this as a visual hint toward a flow, not a workflow diagram or API explanation.

### 4. Check the result; share it when the task grows (1:30)

- Show the review prompt, then the "Need a harness" speaker-page prompt from the motivation table.
- End with the answer-to-artifact spectrum and the C# transition question.
- Keep evidence labels subtle: use the schedule, cite sources, do not guess, and stop when a claim cannot be verified.

### 5. C# Loves AI: three SDK responsibilities (1:30)

- Present responsibilities, not a package catalog:
  - **MEAI (`Microsoft.Extensions.AI`)**: common model-facing abstractions such as `IChatClient`.
  - **MAF (Microsoft Agent Framework)**: agents, tools, and workflow orchestration.
  - **Provider/platform SDKs**: service-specific capabilities and integrations, including Microsoft Foundry.
- Mention the C# MCP SDK as a tool-integration resource later; do not add another live demo.

### 6. Three-step MAF getting started: Prague to the conference schedule (3:30)

- **Agent + tool:** run `agent`. Show `ChatClientAgent`, instructions, and `RunAsync` answering a short welcome question. Then ask about Prague weather and show the actual invocation of the `[Description]` method registered with `AIFunctionFactory.Create`. Say explicitly that the weather is fixed demo data, not a live forecast.
- **Workflow basics (demo 2):** run `workflow Alex`. Show `GetAttendeeProfile -> RecommendSession -> CreateRecommendation`, three delegate executors, `AddEdge`, and `WithOutputFrom`. Point to the printed executor results and the [demo 2 flow](demo-flows.md#demo-2-simple-maf-workflow). Executors do work, edges pass messages, events expose execution; this example needs no LLM or credentials.
- **RAG inside a workflow:** run `rag` with the original AI/.NET schedule question. Show `PrepareQuestion -> AnswerFromSchedule`; the second executor invokes the existing grounded agent. Its `TextSearchProvider` injects the complete schedule Markdown before one model call, without a retrieval tool loop.
- Keep each source excerpt small. Show only workflow event labels and the final grounded answer, never the schedule dump or hidden reasoning.
- Close the introduction with the boundary: an agent can choose tools; code owns the workflow; a context provider supplies evidence. This is full-document grounding, not vector search or a conflict-free agenda planner.
- The [README walkthrough](README.md#demos-in-presentation-order) provides the commands and ordered launch profiles, including decision variants **5.1-5.2** and Harness web search **6.1**. The next segment hosts the same reusable schedule agent, not the weather or simple workflow demos.

### 7. Foundry basics: model and agent are different things (1:30)

- Show the prepared Foundry project and model deployment in the portal.
- Explain where model inference happens and distinguish a model deployment from hosting custom agent code.
- The project, model, and required access are prepared before the talk; do not spend stage time provisioning infrastructure.

### 8. Deploy and invoke the custom agent (5:30)

- Show the schedule agent's Hosted Agent companion and the code-deployment path using Azure Developer CLI (`azd`).
- Deploy the prepared custom .NET Hosted Agent, then invoke it in the Foundry portal to prove the deployed agent responds.
- Timebox the live deploy/invocation. If it exceeds the cap or fails, switch to a prepared capture and invoke the already deployed version. Do not attempt onstage infrastructure creation or permission changes.
- Keep AVM and Terraform out of this talk's path; neither is needed for this demonstration.

### 9. Agentic Design Patterns: overview (2:00)

- Retain the existing pattern-family overview slide; do not replace or redraw it.
- Make the point that real tasks combine different needs, so choosing and composing a pattern is not automatic.
- Narrow the rest of the talk to Planning/Goal Monitoring (as a composed workflow), Decision Models, and the Agent Harness.

### 10. Planning and Goal Monitoring: a self-correcting agenda (2:00)

- Return to the "Add a flow" and "Add review" beats: plan Cédric's day 1 and check it against explicit goals.
- Start from the brief: four interesting day-1 sessions, interests, own talk #13, and AI/.NET coverage. Disclose that the monitor additionally knows two policies: no overlaps and keep 12:30 free for preparation.
- Show the acceptance checklist: exactly 4 distinct day-1 sessions, no overlaps, must attend #13, keep 12:30 free, at least 2 AI sessions besides #13 and 1 .NET session, and a critic fit score of at least 4/5.
- Show the saved [demo 4 graph](demo-flows.md#demo-4-planning-and-goal-monitoring): **Planning + Reflection + Goal Monitoring + Parallelization**. Task planning produces a strategy that a separate agenda builder consumes; the agenda is the resulting artifact. Routing controls replan / publish / escalate. The outer workflow is code-defined, not an autonomous scheduler.

### 11. Planning and Goal Monitoring: live workflow (4:00)

- Show the `WorkflowBuilder` excerpt: `AddFanOutEdge`, `AddFanInBarrierEdge`, three conditional `AddEdge<T>` routes, and `WithOutputFrom(publish, escalate)`. These build on the basic edges from step 2 of getting started.
- Run `plan` live. Narrate each round: task strategy, built agenda, two parallel checks, PASS/FAIL table, route. Both reviewers know all goals. On failure, show how concrete scheduling evidence and critic repair advice enter the next task plan and a fresh agenda. Relevant topics alone must not earn a perfect critique of an invalid schedule.
- If round 1 passes, acknowledge that the model inferred good scheduling; use a captured failing run to demonstrate refinement if needed. `plan --rounds 1` does not force failure. The brief makes mistakes plausible, not guaranteed; disclose the information split rather than blame the model for missing requirements it was never given.
- Emphasize the boundary: code owns hard-goal acceptance; the critic reviews the complete contract and supplies an overall quality score and repair advice. A call budget plus `Escalate` ends the loop deterministically.
### 12. Decision Models: three shapes of judgment (3:00)

- Feed the complete two-day schedule into every task; show its source rather than dumping the document.
- Explain the supported task shapes: **choice** among labels, **Noul** (yes/no), and **score** on an ordered scale.
- Start with "Is Cédric Mendelin speaking on Day 1?" and "Day 2?" for a clear yes/no contrast. The snapshot lists a named session only on Day 1; do not infer speaker Q&A participation. These illustrate Noul, not a need for model-based factual lookup.
- Give a C# team two needs: connect an existing agent to business data and achieve colleague adoption. Choose among eleven speaker/session options, including Cédric's talk, without a hard-coded winner. Contrast implementation, adoption, and broader agent-building coverage.
- Score three non-AI sessions against that same goal: #6 (garbage collection), #18 (HttpClient optimization), and #20 (advanced TypeScript). Use 0 = no useful coverage, 1 = adjacent, 2 = direct coverage of one need, 3 = direct coverage of both. Distinguish useful background from directly addressing agent integration/adoption; do not prescribe exact probabilities.
- Explain that a score is an output from a decision task, not a calibrated measure of real-world accuracy.

### 13. Decision Models: local provider comparison (7:00)

- Run six calls across three scenarios with **Microsoft-Decision-1 live**: two binary assessments, one eleven-way choice, and three four-level scores. Show question/state, RUNNING, and COMPLETED/result for each call in order; avoid raw JSON and usage metadata on stage.
- Use `decisions --compare-openai`: for each question, call Microsoft live, print its result, then call Azure OpenAI Decisions live with the same input and print its result in the same block.
- Point out that the APIs/contracts differ. This is an illustration, not a benchmark or accuracy comparison; do not compare scores as if they were measured on a controlled evaluation.
- Label each provider and show its running/completed status; there is no sample, recording, or replay.

### 14. From workflow to Agent Harness: the speaker page task (2:00)

- New request: "Turn my checked agenda into a page I can share: photo, my talk, chosen sessions, why they fit, and a few current links."
- Contrast with slide 11: there the **code** owned the steps. Here one agent plans its own steps inside a harness, and the code owns its capabilities, limits, and final acceptance.
- Show the finished `agenda.html` (hero with photo, my talk, timeline, further reading) as the target. The layout may change per run; the content contract does not.

### 15. What the harness adds (2:00)

- Show the whole agent as one `AsHarnessAgent(new HarnessAgentOptions { ... })` snippet: four small tools (two schedule tools, `DownloadGitHubAvatar`, `CheckPage`), the **skill** `speaker-agenda-page` (steps and page rules, loaded on demand), a **sandbox folder** for the file tools (`out\speaker-page` only), **web search** on/off, and no plan-approval mode.
- Point out what came for free from the harness: the agent loop, todos, skill tools, file tools, and the hosted `web_search` tool, which Foundry runs server-side and returns as text with citations. Search finds the GitHub username; the guarded `DownloadGitHubAvatar` tool builds the avatar URL in code and saves the photo into the sandbox.
- Explain that the harness constrains and equips one agent. It is not multi-agent collaboration, and it does not decide whether the result is correct.

### 16. Run the Harness demo live (7:00)

- Run `speaker-page --web --open`. Narrate the live tool trace: `load_skill`, `GetSession`/`ListSessions`, `web_search`, `DownloadGitHubAvatar`, `read_skill_resource`, `file_access_write`, and `CheckPage`. If `CheckPage` fails, point out the self-repair: the agent edits the file and checks again.
- Open the generated page in the browser, then show the green success summary: page saved, speaker, photo, own talk, real sessions, no overlaps, and web links.
- Fallback: `speaker-page` without `--web` (no search dependency) or a previously generated page.
### 17. Composition, resources, and close (4:00)

- Return to the agenda assistant as a conceptual composition: grounded schedule data, a goal-monitored planning workflow, and a harnessed agent that turns the result into a verified artifact.
- Be explicit that the talk's demos are separate examples; do not claim this composition is already one integrated runnable product.
- Close with a practical invitation: start with a real task, add a pattern only when it addresses a demonstrated gap, and keep important controls in application code.
- Put the repository QR/link first, followed by a short list of official .NET AI, MAF, Foundry Hosted Agent, Decision Models, and MCP resources.

### 18. Q&A (10:00)

- Keep the final slide available during questions with the repository link and one-sentence takeaway.

## Demo and accuracy guardrails

- The schedule RAG demo is the only one with a Foundry Hosted Agent deployment and portal invocation. The other demos run as local .NET processes.
- The getting-started weather tool returns labeled fixed Prague data, never a current-weather claim. The attendee welcome workflow runs offline; the agent/tool and RAG steps use live Foundry inference.
- Local inference can use the prepared Foundry model; local tools, schedule data, and the speaker profile remain controlled fixtures; web search runs only with `speaker-page --web`.
- The Foundry project and model deployment are prepared in advance. The Hosted Agent runtime's model-inference access is verified in rehearsal; no credentials or role assignments are created onstage.
- The talk's schedule demo, agenda planning workflow, Decision Models story, and speaker-page Harness agent are new conference examples. Existing chapter demos are references, not proof that these integrated examples already exist.
- The only provider comparison is Microsoft-Decision-1 versus Azure OpenAI Decisions, both live and sequential per question. It is not an evaluation or benchmark.
- If the live deployment exceeds its time cap, use the prepared capture and a previously deployed agent invocation. Preserve Q&A time.

## Resources to put on the final slide or linked notes

- [This repository](https://github.com/cedricmendelin/Agentic-Design-Patterns-MAF)
- [Microsoft.Extensions.AI documentation](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/)
- [Microsoft Foundry Hosted Agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Microsoft Decision Models documentation](https://learn.microsoft.com/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure#decision-models)
- [Microsoft MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- The [Update Conference schedule fixture](Update_Conference_Prague_2026_Schedule.md)
