# Hosted Routing

The [routing pattern](../Routing/README.md) behind the Foundry **Responses protocol**. This companion hosts the same MAF graph; it is not a native replacement for routing.

## Request contract

The latest nonempty user message is the request. A structured `Booker`, `Info`, or `Unclear` decision selects a specialist or deterministic clarification. Bookings are simulated, never real reservations. Malformed routing decisions fail explicitly.

Each request is independent and returns only the final answer, not console diagnostics. No conversation history is persisted by the agent. Streaming emits one buffered completed result; cancellation and workflow/provider errors propagate.

## Where to look

- [`Program`](Program.cs): shared Foundry client configuration and Responses registration.
- [`RoutingAgent`](../Routing/RoutingAgent.cs): shared `StatelessWorkflowAgent` adapter.
- [`RoutingWorkflow.Create`](../Routing/RoutingWorkflow.cs): the actual pattern.

## Run and deploy

Follow [common hosting guidance](../../HOSTING.md) for settings, API-key versus managed-identity behavior, packaging, inference RBAC, sensitive telemetry, and agent-scoped cleanup. Azure hosts should use managed identity, not API keys.

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Routing.Hosted -- --deployment reasoning
dotnet test .\dotnet\tests\Routing.Tests
```

`--deployment` is optional. Run azd commands from this project's directory, which contains its `azure.yaml`.

```powershell
azd ai agent invoke chapter02-routing --local --protocol responses 'Book me a hotel in Paris.'
azd ai agent invoke chapter02-routing --local --protocol responses 'What is the highest mountain?'
azd ai agent invoke chapter02-routing --local --protocol responses 'Can you help me with this?'
azd deploy chapter02-routing --no-prompt
azd ai agent show chapter02-routing --output json
```

Before remote invocation, verify the current runtime identity's inference role as described in the common guide; remove `--local` to invoke the deployed service. Stop the local host with Ctrl+C. Sensitive prompt/response capture is disabled in this demo's agent spans.

## Tradeoffs

A successful route is not permission to book or modify anything. Use labeled routing evaluations, preserve the clarification path, and authorize consequential actions deterministically.
