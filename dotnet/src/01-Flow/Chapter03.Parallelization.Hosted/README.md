# Chapter 03 - Hosted Parallelization Agent

Expose the [Chapter 03 parallelization workflow](../Chapter03.Parallelization/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF provides the fan-out/fan-in orchestration; Foundry hosts the agent and model. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter03-parallelization` service.

## What it does

Each request fans the input out to three independent workers and then synthesizes their results. The `research:` mode focuses the workers on renewable energy, electric vehicles, and carbon capture; it uses model knowledge, not live search. Connect a supported retrieval tool when adapting it for current research.

- **Input:** plain text, optionally prefixed with `topic:` or `research:` (prefix matching is case-insensitive). Unprefixed text uses topic analysis. An empty recognized prefix is invalid.
- **Output:** the completed synthesis only; intermediate branch results are not returned to the caller.
- **State:** every request is an independent workflow with no persisted conversation history. The response is buffered until synthesis completes.
- **Failure behavior:** a failed worker or synthesis fails the request rather than returning a partial synthesis.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `topic: How can cities reduce emissions?` | Summary, question-generation, and key-term workers run concurrently, followed by synthesis | One focused answer combining a topic summary, related questions, and key terms without unsupported specifics |
| `research: Compare renewable energy, electric vehicles, and carbon capture for a city.` | Three sustainable-technology specialists analyze the same request from their respective perspectives; a synthesizer combines their notes | A structured report with sections for renewable energy, electric vehicles, carbon capture, and an overall conclusion; it is grounded only in worker summaries, not live research |
| `The history of space exploration` | Missing prefix defaults to topic analysis | A synthesized, focused explanation informed by a concise summary, three related questions, and key terms |
| `research:` | The recognized prefix has no input text | The request is rejected as invalid rather than sent to the workers |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and shared `Foundry` user-secrets ([setup](../../../README.md#configure-foundry-user-secrets)). A blank `Foundry:ApiKey` selects your developer identity through `DefaultAzureCredential`.

From the repository root:

```powershell
Set-Location .\dotnet\src\01-Flow\Chapter03.Parallelization.Hosted
azd ai agent run chapter03-parallelization
# or, without azd:
dotnet run --project Chapter03.Parallelization.Hosted.csproj
```

The local server listens on `http://localhost:8088`; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter03-parallelization --local --protocol responses 'topic: How can cities reduce emissions?'
azd ai agent invoke chapter03-parallelization --local --protocol responses 'research: Compare renewable energy, electric vehicles, and carbon capture for a city.'
azd ai agent invoke chapter03-parallelization --local --protocol responses 'The history of space exploration'
```

Stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for background; all commands below run in this project folder.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\01-Flow\Chapter03.Parallelization.Hosted
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
   azd deploy chapter03-parallelization --no-prompt
   ```

4. **Grant the runtime identity model access.** Run `azd ai agent show chapter03-parallelization --output json`, read `instance_identity.principal_id`, and assign **Cognitive Services OpenAI User** at the model-hosting Foundry account scope if it is missing:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow a few minutes for RBAC propagation and recheck after redeployments.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter03-parallelization --protocol responses 'topic: How can cities reduce emissions?'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 03 and Shared projects. No Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter03.Parallelization` activity source records worker and synthesis agent spans. **Sensitive-data capture is enabled for this demo.** Prompts, worker results, and responses may appear in traces. Use non-sensitive sample data, and disable sensitive capture before handling confidential data. To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter03-parallelization`.

## Costs and cleanup

Hosting and model calls incur Azure charges; parallel workers plus synthesis make more model calls than a single prompt. Remove only the `chapter03-parallelization` agent you own through Foundry's agent management. Don't run `azd down` against a shared project.

## Pattern tradeoffs

Parallel work helps only when subtasks are independent and the model endpoint can handle concurrent calls; rate limits and the slowest branch can dominate latency. Fan-in needs explicit synthesis criteria and a failure policy. When adapting the workflow, decide whether to fail, retry, or return clearly labeled partial results, and preserve attribution so synthesis does not conceal conflicting or unsupported findings.
