# Chapter 19 - Hosted Legal Survey Judge

Expose the [Chapter 19 legal survey judge](../Chapter19.Evaluation/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter19-evaluation` service.

## What it does

Submit one legal survey question as plain-text input. The agent evaluates the latest user message with the shared MAF `LegalSurveyJudge` and returns a JSON judgment containing `overall_score` (1-5), `rationale`, `detailed_feedback`, `concerns`, and `recommended_action`. Feedback must name and address clarity and precision, neutrality and bias, relevance and focus, completeness, and appropriateness for the audience; code validates the structured result.

Each call is independent and uses a fresh judge call. Earlier messages, prior judgments, notebook batches, cloud evaluation jobs, and caller-supplied system instructions do not affect the evaluation. Protocol history may be retained by the platform, but it does not influence grading; use `store: false` for independent calls. There is no durable application-level memory. `stream: false` returns the completed judgment; SSE buffers it and emits one assistant update so partial JSON is not exposed. Errors, cancellation, and invalid output propagate rather than becoming success-shaped fallbacks.

This judge reviews survey methodology. It does not certify legal accuracy, approve a survey for use, or implement Foundry's custom endpoint-evaluator payload schema.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `To what extent do you agree or disagree that current intellectual property laws in Switzerland adequately protect emerging AI-generated content? (Strongly Disagree, Disagree, Neutral, Agree, Strongly Agree)` | Runs the five-criterion judge on a survey question. | JSON judgment with a 1-5 score, concise rationale, actionable feedback naming all five criteria, concerns array, and recommended action. No exact score is guaranteed. |
| `Don't you agree that overly restrictive data privacy laws are hindering essential technological innovation and economic growth? (Yes, No)` | Assesses leading language, loaded terms, and whether the question combines assumptions. | The judgment may identify neutrality/bias and clarity issues; the model's score and wording are not fixed. |
| `What are your thoughts on legal tech?` | Assesses a deliberately vague question. | Structured feedback can identify undefined scope or audience and suggest making the question more focused. |
| `Does the Federal Supreme Court's recent ruling prove that every AI-generated work is protected by Swiss copyright law?` | Evaluates wording and is instructed to flag uncertain legal premises, not verify legal doctrine. | A structured methodology review may flag the asserted premise and need for context; it is not legal advice or a legal fact-check. |

## Run locally

Prerequisites: .NET 10 SDK, Azure CLI, azd with the `azure.ai.agents` extension, a structured-output-capable Foundry chat deployment, and developer identity access to model inference. The hosted judge always uses Entra ID and never uses the console API key. For local setup context, see [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets).

From the repository root:

```powershell
Set-Location .\dotnet\src\05-TrustAndOperations\Chapter19.Evaluation.Hosted
$env:FOUNDRY_PROJECT_ENDPOINT = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = '<chat-model-deployment>'
$env:AZURE_TENANT_ID = '<tenant-id>'
azd ai agent run chapter19-evaluation
# Or, without azd:
dotnet run
```

`Foundry:ProjectEndpoint` and `Foundry:ModelDeployment` in shared user-secrets are local fallbacks; the environment variables above take precedence. Keep the host private while it listens on port 8088 by default. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter19-evaluation --local --protocol responses 'What are your thoughts on legal tech?'
azd ai agent invoke chapter19-evaluation --local --protocol responses 'To what extent do you agree that current intellectual property laws adequately protect emerging AI-generated content?'
azd ai agent invoke chapter19-evaluation --local --protocol responses 'Does this question make an uncertain legal premise?'
```

For a direct local request, preserving the Responses options explicitly:

```powershell
$body = @{
    input = 'How clear are the data-retention requirements in the privacy notice you received? (Very unclear, Unclear, Neither, Clear, Very clear)'
    stream = $false
    store = $false
} | ConvertTo-Json
$response = Invoke-RestMethod 'http://localhost:8088/responses' -Method Post -ContentType 'application/json' -Body $body
$response.output.content.text
```

Stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for prerequisites and background. All commands below run from this project folder.

1. **Sign in and create this folder's azd environment:**

   ```powershell
   Set-Location .\dotnet\src\05-TrustAndOperations\Chapter19.Evaluation.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   ```

2. **Point [`azure.yaml`](azure.yaml) at your project and model.** Replace the sample `mvp-playground-foundry-proj` project key, its endpoint, and the service's `uses` reference with your Foundry project. The manifest maps `AZURE_AI_MODEL_DEPLOYMENT_NAME` into the service; set it to the compatible chat-model deployment as above.

3. **Deploy only this service:**

   ```powershell
   azd deploy chapter19-evaluation --no-prompt
   ```

4. **Verify runtime model access.** Run `azd ai agent show chapter19-evaluation --output json` and read the current `instance_identity.principal_id`. Check effective role assignments, including inherited grants, on the actual model-hosting Foundry account. If inference access is missing and the RBAC change is authorized, assign **Cognitive Services OpenAI User** (`5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) to that runtime principal at the account scope:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope '<model-hosting-account-arm-id>'
   ```

   Allow time for RBAC propagation and recheck after redeployments, which can change the runtime identity.

5. **Invoke the deployed agent after access is effective:**

   ```powershell
   azd ai agent invoke chapter19-evaluation 'What are your thoughts on legal tech?' --new-session
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 19 and Shared projects; no container registry is required.

## Adapting evaluation examples

The hosted service runs only the shared legal survey judge on caller input. It does not replay the console's three-question sample batch, run a Foundry cloud evaluation, or automatically create evaluation resources. A hosted Responses request is not Foundry's custom endpoint-evaluator HTTP contract. The rubric is a methodology aid, and a score is not evidence of legal correctness.

## Telemetry and sensitive data

The `Chapter19.Evaluation` activity source records judge/model work. Sensitive OpenTelemetry capture is enabled for this demo, so survey questions, rubric context, and judgments may appear in traces. **Use synthetic questions and restrict telemetry access and retention; disable sensitive capture before handling confidential surveys without an approved logging policy.** `store: false` does not redact telemetry. To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter19-evaluation`.

## Costs and cleanup

Each request makes a structured model call; hosted compute and enabled telemetry may also incur charges. Use Foundry's agent-scoped controls to remove only the `chapter19-evaluation` agent and versions you own. Never run `azd down` against a shared project.

## Pattern tradeoffs

An explicit rubric makes subjective feedback easier to inspect, but an LLM judge can still be biased, inconsistent, or wrong. Validate the rubric against representative questions and human reviewers; treat the score as review evidence, not legal approval or a release gate.
