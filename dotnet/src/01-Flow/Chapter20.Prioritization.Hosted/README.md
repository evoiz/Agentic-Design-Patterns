# Chapter 20 - Hosted Prioritization

Expose the [Chapter 20 MAF prioritization workflow](../Chapter20.Prioritization/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. Each request contains one or more task descriptions and returns a validated priority queue. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter20-prioritization` service.

## What it does

Send task descriptions as plain text (one task per line) or as a JSON object with a `tasks` array, for example `{"tasks":["Urgent: restore sign-in","Review release notes"]}`. A request must contain 1-20 non-empty tasks. The agent assigns request-local IDs, asks the MAF priority proposer for a priority and assignee per task, validates each proposal, then returns numbered lines in `TASK-### | P0/P1/P2 | assignee | description` form. Tasks sort by P0, then P1, then P2; equal-priority tasks retain arrival order. The proposer is instructed to treat urgent/ASAP/critical work as P0, use P1 when no urgency is stated, and recommend `Worker A` when no available worker is specified. Available workers are `Worker A`, `Worker B`, and `Review Team`.

Each request is isolated: IDs restart for the request, no prior task state or conversation history is reused, and the complete schedule is buffered before it is returned. The service processes caller input rather than replaying the console's sample tasks. An invalid structured proposal is retried once; a second invalid proposal returns an error. Invalid task counts, blank tasks, and malformed JSON are rejected.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Urgent: restore sign-in for affected customers.` | Treats the plain-text message as one task; the proposer assesses urgency. | One numbered task with a request-local ID and validated priority/assignee. The prompt directs urgent work to P0; model proposals are not guaranteed. |
| `Urgent: restore sign-in for affected customers.`<br>`Review the marketing website content.` | Splits non-JSON input into two tasks, proposes each separately, and schedules the validated results. | Two numbered lines, sorted by the returned priorities, with FIFO order for any tie. |
| `{"tasks":["Fix checkout outage","Review the release notes"]}` | Parses a JSON `tasks` array and uses the same proposer, validation, and scheduler. | A complete schedule with one result per task; IDs begin at `TASK-001` for each request. |
| `{"tasks":[]}` or `{"items":["Review release notes"]}` | Exercises invalid task count or wrong JSON shape. | The request is rejected with an input error; it does not return a success-shaped empty queue. |

## Run locally

Prerequisites: .NET 10 SDK, Azure CLI, azd with the `azure.ai.agents` extension, a Foundry chat deployment, and either shared Foundry user-secrets or developer identity access. Local configuration can use shared secrets (`Foundry:ProjectEndpoint`, `Foundry:ModelDeployment`, optional `Foundry:ApiKey`) or their environment overrides. With no API key, local inference uses `DefaultAzureCredential`; Azure uses the hosted runtime identity. See [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets).

From the repository root:

```powershell
Set-Location .\dotnet\src\01-Flow\Chapter20.Prioritization.Hosted
azd ai agent run chapter20-prioritization
# Or, without azd:
dotnet run
```

The service uses the configured default model deployment; `--deployment <alias>` can select an optional configured model alias when running the host directly. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter20-prioritization --local --protocol responses 'Urgent: restore sign-in for affected customers.'
azd ai agent invoke chapter20-prioritization --local --protocol responses '{"tasks":["Urgent: restore sign-in","Review release notes"]}'
azd ai agent invoke chapter20-prioritization --local --protocol responses 'Investigate the database-pool saturation issue.'
```

Stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for prerequisites and background. All commands below run from this project folder.

1. **Sign in and create this folder's azd environment:**

   ```powershell
   Set-Location .\dotnet\src\01-Flow\Chapter20.Prioritization.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   ```

2. **Point [`azure.yaml`](azure.yaml) at your project and model.** Replace the sample `mvp-playground-foundry-proj` project key, its endpoint, and the service's `uses` reference with your Foundry project. Replace the sample model value with `${AZURE_AI_MODEL_DEPLOYMENT_NAME}` so the environment value above selects your chat deployment.

3. **Deploy only this service:**

   ```powershell
   azd deploy chapter20-prioritization --no-prompt
   ```

4. **Verify runtime model access.** Run `azd ai agent show chapter20-prioritization --output json` and read the current `instance_identity.principal_id`. Check effective assignments, including inherited grants, on the actual model-hosting Foundry account. If inference access is missing and the RBAC change is authorized, assign **Cognitive Services OpenAI User** (`5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) to that runtime principal at the account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck after redeployments, which can change the runtime identity.

5. **Invoke the deployed agent after access is effective:**

   ```powershell
   azd ai agent invoke chapter20-prioritization --protocol responses '{"tasks":["Urgent: restore sign-in for affected customers.","Review the marketing website content."]}'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 20 and Shared projects; no Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter20.Prioritization` activity source records schedule activity and task-count/priority-count metadata. The proposer enables sensitive-data capture, so task descriptions, proposals, and assignees may appear in exported traces. **Use non-sensitive task examples and restrict telemetry access and retention; disable sensitive capture before processing confidential tasks.** To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter20-prioritization`.

## Costs and cleanup

There is one structured proposal per task, with at most one retry for an invalid proposal; up to 20 tasks therefore make up to 40 model calls. Hosted compute, model inference, and enabled telemetry may incur charges. Remove only the `chapter20-prioritization` agent and versions you own with Foundry's agent-scoped controls. Do not run `azd down` against a shared project or delete its shared models.

## Pattern tradeoffs

Model proposals can misinterpret urgency or assignment, so queue order is deterministic only after proposal validation; it cannot make the proposal itself authoritative. FIFO tie-breaking is transparent but does not account for deadlines, dependencies, worker capacity, or service-level agreements. Add durable storage and deterministic policy checks when those constraints matter.
