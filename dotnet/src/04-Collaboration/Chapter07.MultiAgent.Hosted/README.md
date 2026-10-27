# Chapter 07 - Hosted Multi-Agent Agent

Expose the [Chapter 07 multi-agent scenarios](../Chapter07.MultiAgent/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host reuses the MAF orchestration and accepts one caller-provided scenario per request. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter07-multi-agent` service.

This is one hosted process with in-process MAF agents. For independent specialist services and an orchestrator making remote calls, see the [distributed crew companion](../Chapter07.MultiAgent.Distributed/README.md).

## What it does

Send a user message as `<scenario>: <task>`. Supported scenarios are `sequential`, `parallel`, `loop`, `coordinator`, `agent-tool`, and `crew` (hyphenated names are accepted case-insensitively). An unrecognized or missing prefix defaults to `crew`, with the entire message passed as its task.

- **Input:** one scenario-prefixed text message; a recognized prefix with an empty task is invalid.
- **Output:** the selected workflow's final result. Intermediate specialist outputs are available in traces, not included separately in the response.
- **State and isolation:** each request creates a fresh workflow and persists no conversation history. `RequestIsolationKeyProvider` partitions MAF session state using the server-generated Responses ID. Service-principal and managed-identity callers do not need a platform user header; this does not bypass Foundry authorization or impersonate a user. Resuming MAF state across responses is unsupported; send a fresh request instead of `previous_response_id`.
- **Simulated behavior:** the coordinator's task executor is deterministic demonstration behavior, and `agent-tool` returns a mock image-asset result; no real task service or image generator is connected. Parallel weather/news specialists have no live data tools.
- **Loop format:** each loop iteration returns structured JSON fields `isComplete`, `result`, and `nextStep`; the workflow requires nonempty progress and a next step when continuing.
- **Failure behavior:** loop work is limited to three iterations. Missing structured progress, invalid task input, provider failures, and failure to complete within the loop limit fail the request.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `sequential: Explain how rain gardens help manage stormwater.` | Researcher produces concise background notes; writer turns those notes into an explanation | One final explanation based on the supplied model-generated notes, with uncertainty preserved |
| `parallel: Analyze how an extreme heat event could affect a city.` | Weather and news specialists analyze in parallel; a coordinator synthesizes the two results | One coordinated answer incorporating both perspectives and their uncertainty; no live weather or news research is claimed |
| `loop: Describe practical ways a school can reduce food waste.` | A structured processor iterates on the task, checking its completion status each time | Final result when marked complete within three iterations; otherwise the request fails at the deterministic limit |
| `coordinator: Say hello to the project team.` | Coordinator delegates the greeting request to the Greeter | A brief greeting from the selected delegate |
| `coordinator: Prepare a checklist for a demo.` | Coordinator delegates a task request to the deterministic `execute_task` function | A completion message from the demonstration executor; no external task is performed |
| `agent-tool: Create a visual prompt for a paper boat on a lake.` | Artist invents a visual prompt and passes it to the ImageGen specialist and mock tool | The response reports a mock image asset; no real image is generated |
| `crew: Explain how teams can use AI agents to analyze a product launch.` | Researcher identifies themes and practical impact; writer produces an article from those notes | Final accessible article of about 350 words based on the supplied notes |
| `Explain how teams can use AI agents to analyze a product launch.` | No prefix defaults to the `crew` scenario | Same researcher-then-writer article shape as the explicit `crew:` request |
| `parallel:` | A recognized prefix has no task text | The request is rejected as invalid rather than running an agent scenario |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and shared `Foundry` user-secrets ([setup](../../../README.md#configure-foundry-user-secrets)). A blank `Foundry:ApiKey` selects your developer identity through `DefaultAzureCredential`. For the azd local runner, ensure the local azd environment or the service's untracked `.env` file supplies `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` consistent with the Foundry settings read by the host.

From the repository root:

```powershell
Set-Location .\dotnet\src\04-Collaboration\Chapter07.MultiAgent.Hosted
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent run chapter07-multi-agent --no-client
# or, without azd:
dotnet run --project Chapter07.MultiAgent.Hosted.csproj
```

The local server listens on `http://localhost:8088`; keep it private. In a second terminal, from the same folder:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter07-multi-agent --local --protocol responses 'parallel: Analyze how an extreme heat event could affect a city.'
azd ai agent invoke chapter07-multi-agent --local --protocol responses 'crew: Explain how teams can use AI agents to analyze a product launch.'
azd ai agent invoke chapter07-multi-agent --local --protocol responses 'agent-tool: Create a visual prompt for a paper boat on a lake.'
```

Stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for background; all commands below run in this project folder.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\04-Collaboration\Chapter07.MultiAgent.Hosted
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
   azd deploy chapter07-multi-agent --no-prompt
   ```

4. **Grant the runtime identity model access.** Run `azd ai agent show chapter07-multi-agent --output json`, read `instance_identity.principal_id`, and verify **Cognitive Services OpenAI User** (or equivalent inference access) at the actual model-hosting Foundry account scope. Assign it if missing:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow a few minutes for RBAC propagation and recheck after redeployments.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter07-multi-agent --protocol responses 'crew: Explain how teams can use AI agents to analyze a product launch.'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 07 and Shared projects. No Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter07.MultiAgent` activity source records agent and workflow spans. **Sensitive-data capture is enabled for this demo.** Caller tasks, specialist outputs, and final responses may appear in traces. Use non-sensitive sample data, and disable sensitive capture before handling confidential data. To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter07-multi-agent`.

## Costs and cleanup

Hosting and model calls incur Azure charges; scenarios with multiple specialists or loop iterations make additional calls. Remove only the `chapter07-multi-agent` agent you own through Foundry's agent management. Don't run `azd down` against a shared project.

## Pattern tradeoffs

Multi-agent orchestration can improve decomposition when responsibilities are distinct, but increases latency, token consumption, and the number of handoffs that can lose context or propagate errors. Parallel specialist agreement is not evidence of correctness. Keep loops bounded and use deterministic checks for mandatory policy and completion rules.
