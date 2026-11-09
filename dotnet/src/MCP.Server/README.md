# MCP greeting server

This is the local HTTP transport companion for [MCP](../MCP/README.md), not a Foundry deployment. It exposes one typed `greet(name)` tool through a stateless MCP endpoint at `/mcp`. Blank names are rejected; a valid name returns a greeting.

From the repository root, start it in a separate terminal:

```powershell
dotnet run --project .\dotnet\src\MCP.Server --urls http://localhost:8000
```

Then run `dotnet run --project .\dotnet\src\MCP -- greet` in another terminal. The server itself needs no model credentials; the client demo uses [shared Foundry settings](../../README.md). Stop the server with Ctrl+C.

The client confines this example to loopback HTTP. Do not expose the unauthenticated sample as a public service. Foundry-native MCP requires a separately reachable trusted endpoint with appropriate authentication; it cannot reach your laptop's localhost.
