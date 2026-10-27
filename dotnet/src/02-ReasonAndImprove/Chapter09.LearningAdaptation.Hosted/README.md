# Chapter 09 - Hosted Learning and Adaptation

Expose the [Chapter 09 MAF prompt adaptation workflow](../Chapter09.LearningAdaptation/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter09-learning-adaptation` service.

## What it does

The caller supplies starting assistant instructions. The host scores them against a fixed three-case support-policy benchmark, and—if the baseline does not pass—asks a curator for one replacement prompt proposal and evaluates it. The response contains baseline and best scores, whether the candidate improved the score, and the best-scoring instructions for human review. This hosted workflow is bounded to one adaptation iteration; it does not replay a sample batch or adopt the proposal.

- **Input:** the complete starting instructions as the latest user message, up to 4,000 characters. No prefix or JSON wrapper is required. Empty/whitespace-only or over-limit instructions are rejected.
- **Output:** a text report with baseline score, best score, number of attempts, improvement status, recommendation, and the best-scoring prompt.
- **State and isolation:** each request is an independent bounded run; previous conversation history is not used to seed it. No candidate is written to disk, deployed, or automatically promoted.
- **Evaluation:** deterministic phrase matching checks each response against the benchmark's required and forbidden phrases. The benchmark cases cover an eligible sealed return, a late return, and a damaged delivery. A passing phrase check is not a semantic quality guarantee.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `You are a helpful customer-support assistant. Answer the customer's question briefly.` | Scores the supplied instructions on all three fixed support cases and may request one candidate update. | Report includes baseline and best case counts, adaptation attempts, improvement status, recommendation, and best-scoring prompt; no exact score is guaranteed. |
| `You are a careful support assistant. Apply only the policy stated in each case, explain return eligibility, and never guarantee an outcome.` | Evaluates the supplied instructions against the eligible-return and late-return policies as well as the damage-information case. | Phrase-based benchmark report and, if needed, one proposed replacement prompt. |
| `You are a support assistant. For damaged deliveries, ask for the order number and a photo of the damage.` | Evaluates the supplied prompt against the fixed damage case and both return cases; it does not run only the referenced scenario. | The output still reports all three cases and the best-scoring instructions. |
| `You are a helpful assistant.` followed by additional text until the complete message exceeds 4,000 characters | Fails the input-size validation before model evaluation. | Request validation error; no score or adaptation proposal is produced. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, a Foundry project and chat-model deployment, and the shared [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets). Locally, a configured API key may be used; if none is configured, the host uses the developer's Entra identity. The hosted runtime uses managed identity.

From the repository root:

```powershell
Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter09.LearningAdaptation.Hosted
azd ai agent run chapter09-learning-adaptation
# or run the Responses host directly:
dotnet run
```

In a second terminal, from the same folder:

```powershell
azd ai agent invoke chapter09-learning-adaptation --local --protocol responses 'You are a helpful customer-support assistant. Answer the customer''s question briefly.'
azd ai agent invoke chapter09-learning-adaptation --local --protocol responses 'You are a careful support assistant. Apply only the policy stated in each case, explain return eligibility, and never guarantee an outcome.'
```

The final response is buffered until evaluation and any single candidate iteration finish. Stop the local host with Ctrl+C.

## Deploy to Foundry

Follow the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents). Run azd commands from this project folder.

1. **Sign in and create an azd environment:**

   ```powershell
   Set-Location .\dotnet\src\02-ReasonAndImprove\Chapter09.LearningAdaptation.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** For more about this configuration, see [point the manifest at your project and model](../../../README.md#point-the-manifest-at-your-project-and-model). Replace the sample `mvp-playground-foundry-proj` project key and endpoint with your project and update `uses`. Change the service's `AZURE_AI_MODEL_DEPLOYMENT_NAME` value to `${AZURE_AI_MODEL_DEPLOYMENT_NAME}` so the azd environment value from step 1 supplies your selected chat deployment.

3. **Deploy:**

   ```powershell
   azd deploy chapter09-learning-adaptation --no-prompt
   ```

4. **Grant runtime model access.** Run `azd ai agent show chapter09-learning-adaptation --output json`, resolve the current `instance_identity.principal_id`, and check effective assignments including inherited grants. If missing, obtain authorization and assign **Cognitive Services OpenAI User** at the actual model-hosting Foundry account scope. Do not substitute the deploying user, project identity, or agent blueprint. Allow time for RBAC propagation and recheck after redeployment.

   ```powershell
   azd ai agent show chapter09-learning-adaptation --output json
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

5. **Invoke after RBAC propagation:**

   ```powershell
   azd ai agent invoke chapter09-learning-adaptation --protocol responses 'You are a helpful customer-support assistant. Answer the customer''s question briefly.'
   ```

Bundled .NET 10 code deployment publishes the host with the referenced Chapter 09 and Shared projects. No Docker image or container registry is required.

## Adapting the evaluation examples

The hosted request runs the fixed three-case benchmark once per evaluation, and at most one curation-and-candidate pass. The phrase-based checks are simple, visible, and inexpensive, but do not establish full semantic correctness. To adapt the workflow, change the benchmark and validation criteria deliberately and add representative regression cases; do not treat a higher score as permission to promote a prompt.

## Telemetry and sensitive data

The `Chapter09.LearningAdaptation` activity source records `learning-adaptation.run` and tags the iteration budget, baseline score, best score, and proposal count; child spans cover model calls. **Sensitive-data capture is enabled for this demo:** caller instructions, benchmark inputs, model outputs, and proposed prompts may appear in traces. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive-data capture before confidential use. To view local spans, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter09-learning-adaptation`.

## Costs and cleanup

Each request uses up to three baseline model evaluations, one curator call, and up to three candidate evaluations, in addition to hosted compute. Longer instructions and outputs increase token use. Remove only the reader-owned hosted agent and versions using Foundry's agent-scoped controls. Never run `azd down` against a shared project or remove shared models or project infrastructure.

## Pattern tradeoffs

Automated adaptation can turn benchmark evidence into a reviewable candidate, but phrase matching can be satisfied without genuinely correct or useful behavior. One bounded iteration limits request cost and leaves promotion under human control; it may not find a passing candidate. Evaluate proposed instructions on broader representative cases before adoption.
