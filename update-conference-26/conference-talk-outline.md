# Agentic AI in C#: From a Useful Answer to a Useful System

## Talk direction

**Audience:** .NET developers curious about agentic AI.  
**Slot:** 60 minutes total: 50 minutes of talk and demos, followed by 10 minutes of Q&A.  
**Thesis:** Start with the task, then add only the structure needed to close a demonstrated gap. Models provide judgment; application code controls evidence, tools, boundaries, and execution.

The story starts with one relatable question about the Update Conference schedule. The audience sees the answer improve as the request and available context grow, then sees separate, practical C# examples for planning, decision models, and collaboration. The closing returns to the schedule assistant as a conceptual composition; it must not imply that all of these demonstrations already form one integrated product.

## Motivation: one question, growing needs

Use the same visual language throughout: simple prompt cards, short answer excerpts, and small context labels. Do not introduce pattern diagrams in the motivation. Use real entries from the [conference schedule](Update_Conference_Prague_2026_Schedule.md); verify any displayed session titles and times against that file.

| Beat | User request / context | What the audience should notice |
|---|---|---|
| Plain LLM | "What should I attend today if I'm interested in AI and .NET?" Context label: **No current schedule**. | A general model does not know this event's current agenda. Do not stage a fabricated confident answer as a fact. |
| Add knowledge (RAG) | Repeat the exact same question. Context label: **Schedule available**. | The answer can now name relevant sessions, but retrieval alone does not guarantee a conflict-free, well-formed personal agenda. If showing a conflict, use an actual parallel slot from the schedule. |
| Add a flow | "Build me a three-session agenda today focused on AI and .NET. Use only schedule entries, explain why each fits, include the times, and avoid overlaps." | The user now needs a sequence of constrained steps, not just relevant snippets. |
| Add review | "Check that agenda against my interests, the schedule evidence, and the no-overlap requirement. Correct unsupported choices or conflicts." | A result needs an explicit check against criteria. Keep the criteria visible; do not imply that a fluent answer is proof of correctness. |
| Need collaboration | "I can't attend [speaker]'s session because of a conflict. Based on their public profile and next announced talks, where else could I hear them? Summarize publicly discussed topics, explain the fit to my AI/.NET interests, cite sources, and say if no future talk can be verified." | This needs bounded research and evidence review. Say "publicly discussed topics," not inferred "passion." Use a speaker and future event only after verifying official or speaker-authored sources. |

End the motivation with a brief, non-prescriptive spectrum: **answer -> grounded answer -> planned and checked work -> bounded collaboration**. It is not a maturity ladder or a claim that every task needs an agent. Transition with: **"How do we build only the structure we need in C#, and where does that code run?"**

## Timing

| Segment | Slides | Time |
|---|---:|---:|
| Intro and motivation | 1-4 | 5 min |
| C# Loves AI: SDK roles and local schedule demo | 5-6 | 5 min |
| Foundry basics, deployment, and portal invocation | 7-8 | 7 min |
| Agentic Design Patterns overview | 9 | 2 min |
| Planning and Monitoring | 10-11 | 6 min |
| Decision Models | 12-13 | 10 min |
| Multi-Agent Collaboration and Harness | 14-16 | 11 min |
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
- Contrast it with the prior question: the user now specifies constraints and desired structure. Show a real schedule overlap only if verified.
- Use this as a visual hint toward a flow, not a workflow diagram or API explanation.

### 4. Check the result; research when the question grows (1:30)

- Show the review prompt, then the speaker-research prompt from the motivation table.
- End with the answer-to-collaboration spectrum and the C# transition question.
- Keep evidence labels subtle: use the schedule, cite sources, do not guess, and stop when a claim cannot be verified.

### 5. C# Loves AI: three SDK responsibilities (1:30)

- Present responsibilities, not a package catalog:
  - **MEAI (`Microsoft.Extensions.AI`)**: common model-facing abstractions such as `IChatClient`.
  - **MAF (Microsoft Agent Framework)**: agents, tools, and workflow orchestration.
  - **Provider/platform SDKs**: service-specific capabilities and integrations, including Microsoft Foundry.
- Mention the C# MCP SDK as a tool-integration resource later; do not add another live demo.

### 6. Run the schedule assistant locally (3:30)

- Show a small MAF agent using the schedule Markdown as its source and a read-only retrieval function.
- Run it from a local .NET process with live Foundry inference. Display only the final answer; do not print retrieved passages.
- Explain the boundary in one sentence: the model formulates the response, while the application supplies the schedule evidence.
- The example is a focused schedule demo, not a production search system or a claim that the existing chapter sample already uses this file.

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
- Narrow the rest of the talk to Planning/Monitoring, Decision Models, and Collaboration/Harness.

### 10. Planning and Monitoring: choose dinner, then check the goal (2:00)

- Use the accepted everyday example: choose a simple dinner using explicit preferences and pantry data, then return only the missing shopping items.
- Show the goal as a small checklist: selected meal fits the stated preference; every required ingredient is accounted for; the output contains only missing items.
- Use fixed local data and fake/read-only tools. Do not add a route, budget, or other unrelated feature.

### 11. Planning and Monitoring: local MAF demo (4:00)

- Show one compact `WorkflowBuilder` excerpt, then run the complete local demo.
- Narrate only the key transitions: gather the fixed inputs, choose a meal, check pantry contents, validate the goal, return the shopping list.
- Keep the visible result focused on the shopping list with a concise goal-check status. The shopping workflow is a new talk demo, not an existing repository sample.

### 12. Decision Models: three shapes of judgment (3:00)

- Reuse one session abstract and one explicitly stated attendee interest.
- Explain the supported task shapes: **choice** among labels, **Noul** (yes/no), and **score** on an ordered scale.
- Use "Is this about Foundry?" or "Is this about .NET performance optimization?" only as a quick agenda hook. Exact schedule facts remain deterministic lookups; do not suggest a model is needed for simple factual retrieval.
- Explain that a score is an output from a decision task, not a calibrated measure of real-world accuracy.

### 13. Decision Models: local provider comparison (7:00)

- Run the three decision shapes locally with **Microsoft-Decision-1 live**.
- Show a clearly labeled, previously captured **Azure OpenAI Decisions** result for the same input and task shapes.
- Point out that the APIs/contracts differ. This is an illustration, not a benchmark or accuracy comparison; do not compare scores as if they were measured on a controlled evaluation.
- Use the provider's actual response fields and label captured output as captured.

### 14. Collaboration: resolve the speaker conflict (2:00)

- Return to the agenda story: a relevant speaker's session conflicts with the attendee's chosen agenda.
- Frame the task as finding a verifiable next announced talk and explaining fit from public evidence, not inferring a speaker's private interests.
- Show the source pack boundary: official or speaker-authored snapshots, URLs, and retrieval dates; no live web search during the talk.

### 15. Harness and orchestration are separate (2:00)

- Show two role cards: **Researcher** and **Reviewer**, with one handoff.
- Put the MAF Harness boundary around the Researcher only. Show the code-owned `WorkflowBuilder` outside that boundary.
- The Researcher has bounded, read-only source tools. The Reviewer receives the draft and cited excerpts but has no search/action tools.
- Explain that the harness constrains an agent's execution; it is not another name for multi-agent collaboration.

### 16. Run the bounded collaboration demo locally (7:00)

- Run the fixed source-pack scenario locally with live model inference.
- Show a reviewer flagging one unsupported claim or citation, at most one researcher revision, then a final evidence/citation check.
- If no official future talk is verifiable, the correct result says so. Use a pre-verified speaker/event fixture and a prepared capture if a live response is not presentation-safe.
- Show observable artifacts and citations, not hidden chain-of-thought.

### 17. Composition, resources, and close (4:00)

- Return to the agenda assistant as a conceptual composition: grounded schedule data, explicit planning constraints, a bounded check, and research only when a conflict requires it.
- Be explicit that the talk's demos are separate examples; do not claim this composition is already one integrated runnable product.
- Close with a practical invitation: start with a real task, add a pattern only when it addresses a demonstrated gap, and keep important controls in application code.
- Put the repository QR/link first, followed by a short list of official .NET AI, MAF, Foundry Hosted Agent, Decision Models, and MCP resources.

### 18. Q&A (10:00)

- Keep the final slide available during questions with the repository link and one-sentence takeaway.

## Demo and accuracy guardrails

- The schedule RAG demo is the only one with a Foundry Hosted Agent deployment and portal invocation. The other demos run as local .NET processes.
- Local inference can use the prepared Foundry model; local tools, schedule/recipe data, and speaker source snapshots remain controlled fixtures.
- The Foundry project and model deployment are prepared in advance. The Hosted Agent runtime's model-inference access is verified in rehearsal; no credentials or role assignments are created onstage.
- The talk's schedule demo, shopping workflow, Decision Models story, and speaker collaboration are new conference examples. Existing chapter demos are references, not proof that these integrated examples already exist.
- The only provider comparison is Microsoft-Decision-1 live versus a labeled Azure OpenAI Decisions capture. It is not an evaluation or benchmark.
- If the live deployment exceeds its time cap, use the prepared capture and a previously deployed agent invocation. Preserve Q&A time.

## Resources to put on the final slide or linked notes

- [This repository](https://github.com/cedricmendelin/Agentic-Design-Patterns-MAF)
- [Microsoft.Extensions.AI documentation](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/)
- [Microsoft Foundry Hosted Agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
- [Microsoft Decision Models documentation](https://learn.microsoft.com/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure#decision-models)
- [Microsoft MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- The [Update Conference schedule fixture](Update_Conference_Prague_2026_Schedule.md)
