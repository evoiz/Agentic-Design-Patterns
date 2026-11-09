# Hosted A2A Agent

The server required by the [Chapter 15 console client](../A2A/README.md). It publishes a card and exposes the same MAF specialist through both A2A bindings.

## Where to look in code

[Program.cs](Program.cs), `CreateAgentCard` and the `RegisterProtocol` block: discovery metadata and HTTP+JSON / JSON-RPC endpoint mappings. [InterAgentAgent.Create](../A2A/InterAgentAgent.cs) owns the model behavior.

## Request contract

- The host publishes `/.well-known/agent-card.json` with `weather-guidance`, `text/plain` input/output, and JSON-RPC plus HTTP+JSON bindings. The card advertises metadata; it never sends a task.
- Synchronous JSON-RPC returns a message; HTTP+JSON streaming returns incremental task updates. The streaming-aware task mode is intentional.
- Fresh non-persistent agent runs; no cross-request conversation history.
- Weather/rates/calendar requests demonstrate the protocol only: no live data tools or calendar actions.
- Cancellation and model failures propagate.

## Local commands

Configure [shared model settings](../../README.md), then from the repository root:

```powershell
$env:PORT = '5090'
$env:A2A_PUBLIC_URL = 'http://localhost:5090/'
dotnet run --project dotnet\src\A2A.Hosted
```

In another terminal:

```powershell
dotnet run --project dotnet\src\A2A -- all http://localhost:5090/
```

`PORT` defaults to 8088; `A2A_PUBLIC_URL` must be an absolute HTTP(S) URL and defaults to localhost on that port. The local server binds to all interfaces and does not authenticate callers: use a trusted network/firewall, never expose it publicly as-is.

## Foundry deployment

Follow [HOSTING.md](../../HOSTING.md) for reader-owned project/model selection, bundled .NET publishing, permissions, runtime inference RBAC, sensitive telemetry, cost, and agent-scoped cleanup. Local credentials can use an API key; a Foundry-hosted runtime explicitly uses managed identity and ignores keys.

After that setup, run azd commands from this project's directory, which contains its `azure.yaml`:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd deploy chapter15-a2a --no-prompt
azd ai agent show chapter15-a2a --output json
# Verify current runtime inference access per HOSTING.md before invoking.
azd ai agent invoke chapter15-a2a --protocol a2a --new-session --new-conversation "What is the weather in Paris?"
```

Use Foundry's managed card/endpoint for the deployed service. The console resolver expects standard well-known discovery on independently hosted servers. Foundry controls deployed caller access; A2A metadata alone establishes neither identity nor authorization.
