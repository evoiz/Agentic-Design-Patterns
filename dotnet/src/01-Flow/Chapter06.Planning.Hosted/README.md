# Chapter 06 - Hosted Planning Agent

Expose the [Chapter 06 plan-and-write workflow](../Chapter06.Planning/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF orchestrates the planner and writer; Foundry hosts the agent and model. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter06-planning` service.

## What it does

The planner returns structured output containing an ordered `Steps` list, which deterministic code checks before passing it to the writer. The writer follows that plan to produce an informative summary of about 200 words.

- **Input:** the latest user message as the topic; no prefix is required. Blank input is invalid.
- **Output:** the validated plan and summary, formatted under `### Plan` and `### Summary` headings.
- **State:** each request creates fresh agents and does not persist conversation history. The completed response is buffered until both model calls finish; configured traces expose intermediate stages.
- **Validation and failure behavior:** the planner's structured output must contain 3–6 nonempty steps. Invalid plans, empty summaries, provider failures, and cancellation fail the request.

The writer is instructed to follow the plan and avoid unsupported specifics. This companion hosts the plan-and-write scenario; citation-backed web research is a separate Foundry Deep Research example in the console chapter.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Plan and summarize the role of reinforcement learning in robotics.` | Planner creates and code validates a bounded ordered plan; writer follows it | Response has a `### Plan` list of 3–6 steps and a `### Summary` with an informative summary of about 200 words |
| `Explain how urban trees can reduce heat-island effects.` | Planner and writer apply the same two-stage workflow to a different topic | The plan and summary are both present, with the summary following the supplied plan |
| `Outline the history of space exploration and summarize its major milestones.` | The full user message is treated as the topic, without prefix parsing | A validated plan precedes a concise, approximately 200-word summary |
| *(empty message)* | The host validates the latest user message before starting the workflow | Blank input is rejected; no plan or summary is generated |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and shared `Foundry` user-secrets ([setup](../../../README.md#configure-foundry-user-secrets)). A blank `Foundry:ApiKey` selects your developer identity through `DefaultAzureCredential`.

From the repository root:

```powershell
Set-Location .\dotnet\src\01-Flow\Chapter06.Planning.Hosted
azd ai agent run chapter06-planning
# or, without azd:
dotnet run --project Chapter06.Planning.Hosted.csproj
```

The local server listens on `http://localhost:8088`; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter06-planning --local --protocol responses 'Plan and summarize the role of reinforcement learning in robotics.'
azd ai agent invoke chapter06-planning --local --protocol responses 'Explain how urban trees can reduce heat-island effects.'
```

Stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for background; all commands below run in this project folder.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\01-Flow\Chapter06.Planning.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<model-deployment>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** Replace the sample `mvp-playground-foundry-proj` key and endpoint with your project and update `uses`. Replace the sample `gpt-5-mini` model deployment with your model deployment (or use `${AZURE_AI_MODEL_DEPLOYMENT_NAME}` and the environment value set above).

3. **Deploy:**

   ```powershell
   azd deploy chapter06-planning --no-prompt
   ```

4. **Grant the runtime identity model access.** Run `azd ai agent show chapter06-planning --output json`, read `instance_identity.principal_id`, and assign **Cognitive Services OpenAI User** at the model-hosting Foundry account scope if it is missing:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow a few minutes for RBAC propagation and recheck after redeployments.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter06-planning --protocol responses 'Plan and summarize the role of reinforcement learning in robotics.'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 06 and Shared projects. No Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter06.Planning` activity source records planner, writer, and workflow spans. **Sensitive-data capture is enabled for this demo.** Caller topics, plans, and responses may appear in traces. Use non-sensitive sample data, and disable sensitive capture before handling confidential data. To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter06-planning`.

## Costs and cleanup

Hosting and model calls incur Azure charges; every request makes a planning call and a writing call. Remove only the `chapter06-planning` agent you own through Foundry's agent management. Don't run `azd down` against a shared project.

## Pattern tradeoffs

A well-formed plan can still omit constraints, choose unnecessary steps, or rely on incorrect facts. Validate domain requirements separately from plan structure and ground factual output in appropriate evidence. Planning adds latency and model usage before execution; use deterministic workflows for mandatory business rules, permissions, and state transitions, and treat a generated plan as a proposal rather than authorization to act.
