# Hosted Multi-Agent Collaboration

The [six collaboration scenarios](../MultiAgent/README.md) behind Foundry's **Responses protocol**, in one hosted process. The [distributed crew](../MultiAgent.Distributed/README.md) is the advanced alternative with independently hosted specialists.

## Request contract

Send `<scenario>: <task>` with `sequential`, `parallel`, `loop`, `coordinator`, `agent-tool` (also `agenttool`), or `crew`. Prefixes are case-insensitive. Unrecognized or absent prefixes preserve the whole input and default to `crew`; a recognized prefix without a task fails.

Each request creates a fresh run and returns the selected scenario's actual final result. Streaming emits one buffered result, not intermediate tokens. There is no persisted agent conversation history; provider failures, empty output, cancellation, and loop-budget exhaustion fail explicitly. The loop is limited to three iterations.

`RequestIsolationKeyProvider` partitions MAF session state by the **server-generated response ID**, including managed-identity callers without a user header. It does not bypass Foundry authorization or impersonate a user. Cross-response state resumption is intentionally unsupported: send fresh requests rather than `previous_response_id`.

## Where to look

- [`MultiAgentHostedAgent.ParseRequest`](MultiAgentHostedAgent.cs): preserved request syntax and shared stateless adapter.
- [`RequestIsolationKeyProvider`](RequestIsolationKeyProvider.cs): explicit per-response partitioning.
- [`MultiAgentWorkflow.RunAsync`](../MultiAgent/MultiAgentWorkflow.cs): dispatch; the chapter guide maps scenarios to their small implementation files.

## Run and deploy

See [common hosting guidance](../../HOSTING.md) for configuration, identity, inference RBAC, packaging, telemetry, and safe cleanup. From the repository root:

```powershell
dotnet run --project .\dotnet\src\MultiAgent.Hosted
dotnet test .\dotnet\tests\MultiAgent.Tests
```

Optional host argument: `-- --deployment <alias>`. Alternatively run `azd ai agent run chapter07-multi-agent --no-client` from this project's directory, supplying aligned `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` in the selected azd environment.

From `dotnet`:

```powershell
azd ai agent invoke chapter07-multi-agent --local --protocol responses 'parallel: Analyze an extreme heat event.'
azd deploy chapter07-multi-agent --no-prompt
azd ai agent show chapter07-multi-agent --output json
```

Verify current runtime inference access before remote calls; omit `--local` for the deployed service. Keep the local listener private and stop it with Ctrl+C. Sensitive prompt/response capture under `MultiAgent` is disabled by default.

## Tradeoffs

Multiple agents add cost, latency, context loss, and inconsistent conclusions. Separate personas and parallel agreement do not prove correctness. Keep authorization deterministic and use remote boundaries only when independent capability/lifecycle isolation is needed.
