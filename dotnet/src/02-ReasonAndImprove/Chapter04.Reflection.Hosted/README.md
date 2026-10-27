# Chapter 04 - Hosted Reflection Agent

Expose the [Chapter 04 reflection workflow](../Chapter04.Reflection/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF provides the bounded generator/verifier graph; Foundry hosts the agent and model. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter04-reflection` service.

## What it does

A request selects one of three generator/verifier tasks. The verifier returns structured output with a `Decision` (`Accept` or `Revise`) and nonempty `Reasoning`; revision feedback is sent back to the generator while the round budget remains.

- **Input:** `fact-check: <topic>`, `product: <details>`, or `code-review: <Python task>`. Prefix matching is case-insensitive. Unprefixed text defaults to product description. An empty recognized prefix is invalid.
- **Output:** outcome (`Accepted` or `MaxRoundsReached`), rounds used, final draft, and the review decision and reasoning. A draft at the round limit is explicitly not accepted.
- **State:** each request creates fresh agents and no conversation history is persisted. The final response is buffered until the workflow completes; intermediate stages are available in configured traces.
- **Failure behavior:** malformed structured reviews, empty drafts, provider failures, and cancellation fail the request.

The product scenario deliberately gives the writer misleading length/style guidance while the verifier enforces the house style: at most 40 words, a final sentence exactly `Made for the way you live.`, and no claims beyond the supplied product details. The default budget is three rounds (up to six generator/verifier calls, excluding provider retries).

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `fact-check: Explain why plants use photosynthesis.` | A generator drafts a paragraph; a fact-checker reviews it using model knowledge, without browsing | The final response includes the draft, review, rounds, and accepted or max-rounds outcome; no external verification is claimed |
| `product: A ceramic mug keeps coffee hot and is controlled by a smartphone app.` | A writer drafts copy and a verifier checks the explicit word-count, closing-sentence, and supported-claims criteria | Accepted copy meets the verifier's criteria, or the bounded result reports `MaxRoundsReached` and says the draft was not accepted |
| `code-review: Write a Python factorial function with a docstring, 0! = 1, and ValueError for negative input.` | A Python generator is reviewed against the caller's requirements, including edge cases and documentation | The response contains Python code, reviewer reasoning, and whether the reviewer accepted it within the round budget; it does not claim code execution |
| `A compact insulated water bottle made from stainless steel.` | Missing prefix defaults to the product scenario | The product writer/verifier loop returns a final draft and review outcome |
| `code-review:` | The recognized prefix has no task text | The request is rejected as invalid |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, and shared `Foundry` user-secrets ([setup](../../../README.md#configure-foundry-user-secrets)). A blank `Foundry:ApiKey` selects your developer identity through `DefaultAzureCredential`.

From the repository root:

```powershell
Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter04.Reflection.Hosted
azd ai agent run chapter04-reflection
# or, without azd:
dotnet run --project Chapter04.Reflection.Hosted.csproj
```

The local server listens on `http://localhost:8088`; keep it private. In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter04-reflection --local --protocol responses 'fact-check: Explain why plants use photosynthesis.'
azd ai agent invoke chapter04-reflection --local --protocol responses 'product: A ceramic mug keeps coffee hot and is controlled by a smartphone app.'
azd ai agent invoke chapter04-reflection --local --protocol responses 'code-review: Write a Python factorial function with a docstring, 0! = 1, and ValueError for negative input.'
```

Stop the server with Ctrl+C.

## Deploy to Foundry

See the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents) for background; all commands below run in this project folder.

1. **Sign in and create an azd environment** (stored in this folder's `.azure`):

   ```powershell
   Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter04.Reflection.Hosted
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
   azd deploy chapter04-reflection --no-prompt
   ```

4. **Grant the runtime identity model access.** Run `azd ai agent show chapter04-reflection --output json`, read `instance_identity.principal_id`, and assign **Cognitive Services OpenAI User** at the model-hosting Foundry account scope if it is missing:

   ```powershell
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

   Allow a few minutes for RBAC propagation and recheck after redeployments.

5. **Invoke:**

   ```powershell
   azd ai agent invoke chapter04-reflection --protocol responses 'product: A compact insulated water bottle made from stainless steel.'
   ```

Bundled .NET 10 code deployment publishes this host with the referenced Chapter 04 and Shared projects. No Docker image or container registry is required.

## Adapting evaluation examples

[eval.yaml](eval.yaml), the [sample dataset](datasets/chapter04-reflection-smoke/chapter04-reflection-smoke_dg.jsonl), and the [rubric](evaluators/chapter04-reflection-smoke/rubric_dimensions.json) are starting points for a task-specific evaluation. Align examples and scoring with the request contract, outcome fields, acceptance criteria, and Python tasks before use. Review prefix matching, sentence-count rules, and code-format requirements rather than assuming they are requirements of the reflection pattern. Synthetic candidate responses are examples, not measured agent outputs. Use your own project, model, dataset, and evaluator references when configuring evaluations; evaluation runs incur additional model usage.

## Telemetry and sensitive data

The `Chapter04.Reflection` activity source records generator, verifier, and workflow spans. **Sensitive-data capture is enabled for this demo.** Prompts, drafts, reviews, and responses may appear in traces. Use non-sensitive sample data, and disable sensitive capture before handling confidential data. To view spans locally, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter04-reflection`.

## Costs and cleanup

Hosting and model calls incur Azure charges; each reflection round adds generation and review calls. Remove only the `chapter04-reflection` agent you own through Foundry's agent management. Don't run `azd down` against a shared project.

## Pattern tradeoffs

A model-based reviewer can reinforce the generator's errors; a separate role is not independent evidence. Ground factual claims in trusted sources, run generated code through deterministic checks, and use human review for consequential decisions. Revision loops add latency and cost without guaranteeing improvement, so keep a bounded budget and distinguish reviewer acceptance from budget exhaustion.
