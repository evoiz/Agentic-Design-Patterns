# Hosted Guardrails

This service exposes the [guardrail policy workflow](../Guardrails/README.md) through Foundry Responses. Hosted requests use the conservative policy variant by default; tool-parameter authorization and Azure Content Safety remain separate console scenarios.

## Request contract

Send nonempty plain text to use the conservative default, or select the preserved notebook behavior with JSON:

```json
{
  "text": "A request that needs more context.",
  "policyVariant": "Conservative"
}
```

`policyVariant` is optional and accepts `Conservative` or `LegacyNotebook`. The legacy variant preserves the notebook's uncertain-means-compliant behavior and is never selected implicitly. Invalid JSON, unsupported variant values, empty text, or malformed model results fail before the primary assistant runs.

The policy evaluator returns one of `compliant`, `non-compliant`, or `requires-review`. Only `compliant` reaches the assistant. Non-compliant requests are blocked; requires-review returns a clarification/review response and does not call the assistant. Each call creates fresh agents, forwards only that request's text, and retains no application conversation state. The response partition uses the response ID even when no platform user header is supplied. Streaming buffers one completed response; cancellation and errors propagate.

Tool identity validation is intentionally independent: the policy model cannot override server-owned identity checks. Hosted requests do not invoke the separate profile tool or Content Safety service.

## Local commands

Configure [shared model settings](../../README.md). Start the host:

```powershell
dotnet run --project dotnet\src\Guardrails.Hosted
```

Invoke it from a second terminal:

```powershell
azd ai agent invoke chapter18-guardrails --local --protocol responses "Explain the benefits of unit testing."
azd ai agent invoke chapter18-guardrails --local --protocol responses '{"text":"An uncertain request.","policyVariant":"LegacyNotebook"}'
```

## Deploy and operate

Follow [HOSTING.md](../../HOSTING.md) for reader-owned project/model setup, managed identity and inference RBAC, packaging, costs, and scoped cleanup. From this project's directory:

```powershell
azd deploy chapter18-guardrails --no-prompt
azd ai agent show chapter18-guardrails --output json
# Verify current runtime inference access before invoking.
azd ai agent invoke chapter18-guardrails --protocol responses "Explain the benefits of unit testing."
```

Sensitive prompt/response telemetry is disabled by default. To opt in for a controlled debugging environment, set `Foundry__Telemetry__IncludeSensitiveData=true` for the host. Policy calls add latency/cost and can misclassify; they do not replace deterministic authorization or human approval.
