# Chapter 19 — Hosted Legal Survey Judge

Expose the [shared five-criterion judge](../Chapter19.Evaluation/README.md) using Foundry Responses. This is survey-methodology review, not legal certification or Foundry's custom endpoint-evaluator payload contract.

## Where to look in code

[SurveyEvaluationAgent.cs](SurveyEvaluationAgent.cs), `RunAsync`: caller question → validated JSON judgment, using the shared stateless adapter. [LegalSurveyJudge.EvaluateAsync](../Chapter19.Evaluation/LegalSurveyJudge.cs) owns the rubric; [SurveyJudgment.Validate](../Chapter19.Evaluation/Models/SurveyJudgment.cs) owns the contract. [Program.cs](Program.cs) deliberately retains **Entra-only** authentication rather than the shared API-key-capable factory.

## Request contract

The latest nonempty user `input` is a legal survey question. Output JSON contains:

- `overall_score`: integer 1–5;
- `rationale`, `recommended_action`: nonempty strings;
- `detailed_feedback`: at least five strings naming all rubric criteria;
- `concerns`: array, possibly empty.

No prior judgments, caller system instructions, notebook batches, or cloud evaluation jobs enter this request. Protocol infrastructure can retain history, but grading ignores it; use `store: false` for independent requests. Streaming buffers complete JSON into one assistant update. Invalid output, model errors, and cancellation propagate rather than producing a success-shaped fallback.

## Local commands

Use .NET 10 and a structured-output-capable deployment. Sign in with an Entra identity authorized for inference; the host **never uses the shared console API key**. Runtime variables take precedence over shared endpoint/model user-secrets.

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = '<model-deployment>'
$env:AZURE_TENANT_ID = '<tenant-id>'
dotnet run --project dotnet\src\05-TrustAndOperations\Chapter19.Evaluation.Hosted
```

In another terminal, from this project's directory:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter19-evaluation --local --protocol responses "How clear are the data-retention requirements in the privacy notice?"
```

## Deploy and operate

Complete [HOSTING.md](../../../HOSTING.md): reader-owned project/model, bundled .NET dependencies, managed identity/inference RBAC, sensitive traces/data retention, charges, and scoped cleanup. After setup, from this project's directory:

```powershell
azd deploy chapter19-evaluation --no-prompt
azd ai agent show chapter19-evaluation --output json
# Verify runtime inference access before invoking.
azd ai agent invoke chapter19-evaluation --protocol responses "How clear are the data-retention requirements in the privacy notice?"
```

Judges can share model biases and miss legal errors. Calibrate with human reviewers; a schema-valid score does not approve the survey. Keep dataset/batch evaluation in the separate console tooling, where cloud persistence and evaluator permissions are explicit.
