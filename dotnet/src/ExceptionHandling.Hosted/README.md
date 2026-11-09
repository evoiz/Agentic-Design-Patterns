# Hosted Exception Handling and Recovery

Exposes the [Chapter 12 recovery workflow](../ExceptionHandling/README.md) through the Foundry Responses protocol (`ExceptionHandlingAgent` derives from the shared `StatelessWorkflowAgent`). Build, deploy, inference RBAC, and cleanup steps are in the [hosting guide](../../HOSTING.md).

## Request contract

- Send a location address as the user message; each request starts with fresh workflow state and no history.
- Supported sample addresses: `1600 Amphitheatre Parkway, Mountain View, CA` and `1 Market Street, San Francisco, CA`.
- Prefix `transient:` to simulate one transient failure (retry) or `primary-unavailable:` to exhaust retries and use the city fallback. These are simulation controls, not production flags.
- The response is buffered and states whether the precise or fallback path was used. Model errors and cancellation propagate as request failures.

## Run and invoke

```powershell
dotnet run --project .\dotnet\src\ExceptionHandling.Hosted
azd ai agent invoke chapter12-exception-handling --local --protocol responses 'primary-unavailable: 1 Market Street, San Francisco, CA'
azd deploy chapter12-exception-handling --no-prompt
azd ai agent invoke chapter12-exception-handling --protocol responses 'transient: 1600 Amphitheatre Parkway, Mountain View, CA'
```

## Telemetry

Agent spans use `ExceptionHandling`; sensitive prompt/response capture is disabled by default.
