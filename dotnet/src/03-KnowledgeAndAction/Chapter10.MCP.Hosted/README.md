# Chapter 10 — Hosted MCP Agent

Exposes the [Chapter 10 MCP agent](../Chapter10.MCP/README.md) through the Foundry Responses protocol (`McpHostedAgent` derives from the shared `StatelessWorkflowAgent`). Build, deploy, inference RBAC, and cleanup steps are in the [hosting guide](../../../HOSTING.md).

## Request contract

- The caller's latest message is sent to the MCP-enabled agent.
- Each request opens a **fresh** connection to `CHAPTER10_MCP_SERVER_URL`, discovers tools, and exposes only `greet`.
- No MCP session or conversation state is kept; streaming returns the buffered final result. Cancellation and MCP/model/tool failures propagate.
- Remote Streamable HTTP servers only — the console stdio/filesystem demo is intentionally not hosted.

## Configuration

```powershell
$env:CHAPTER10_MCP_SERVER_URL = "https://<trusted-mcp-server>/mcp"   # local run
azd env set CHAPTER10_MCP_SERVER_URL "https://<trusted-mcp-server>/mcp"  # deployment (run from this directory)
```

The server must be reachable from the deployed host — Foundry cannot reach a developer's `localhost`. Configure server credentials through an approved identity or secret store; never commit tokens.

## Run and invoke

```powershell
dotnet run --project .\dotnet\src\03-KnowledgeAndAction\Chapter10.MCP.Hosted
azd ai agent invoke chapter10-mcp --local "Use the greet tool to greet Ada."
azd deploy chapter10-mcp --no-prompt
azd ai agent invoke chapter10-mcp --protocol responses "Use the greet tool to greet Ada."
```

## Telemetry and safety

Model calls emit spans under `Chapter10.MCP` with **sensitive-data capture enabled**; prompts, tool inputs, and results may appear in traces. Treat the endpoint, its tool descriptions, and results as untrusted; use least-privilege server credentials and require approval for tools that read sensitive data or make changes.
