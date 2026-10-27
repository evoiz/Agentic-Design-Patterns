# Chapter 10 - Hosted MCP Agent

Expose the [Chapter 10 MAF MCP agent](../Chapter10.MCP/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host connects to a remote Streamable HTTP MCP server and exposes only its allow-listed `greet` tool. This folder is self-contained for deployment: its own [`azure.yaml`](azure.yaml) defines the `chapter10-mcp` service.

## What it does

The host passes the latest caller message to an MCP-enabled MAF agent. For each request it opens a fresh MCP connection, discovers server tools, filters them to `greet`, and returns the agent's final response. The sample greeting tool returns a greeting and has no external side effects.

- **Input:** non-empty plain-text request; no prefix or JSON wrapper is required. Use a request that asks the agent to greet someone to trigger the sample tool.
- **Output:** one buffered final text response after MCP tool execution completes.
- **State and isolation:** no conversation history or MCP session state is persisted by this host; each request creates a new connection and run.
- **Failures:** cancellation, MCP, model, and tool failures propagate. The MCP endpoint must support Streamable HTTP and be reachable from both the local host and deployed Foundry host.
- **Scope and safety:** the hosted companion only supports the remote HTTP MCP flow and only allows `greet`. Tool names, descriptions, arguments, and results are untrusted data. Review endpoint and tool policy before deployment. Do not treat the demo as a security model for arbitrary MCP servers.
- **Separate console demonstrations:** the Chapter 10 console project also has a local stdio filesystem example, restricted to a dedicated demo directory and list/read/write tools. It is not exposed through this hosted service. `Chapter10.MCP.Server` is a separate local MCP server project with no `azure.yaml`; it is not deployed with this hosted agent.

## Try it: example prompts

| Prompt | What happens | Expected result |
|---|---|---|
| `Use the greet tool to greet Ada.` | The agent calls the allow-listed `greet` tool on the configured MCP server. | Tool returns the deterministic greeting `Hello, Ada! Nice to meet you.`; the agent reports the tool result. |
| `Please greet Morgan using the greeting tool.` | Same request flow, with the name supplied in natural language. | Greeting result for Morgan, returned as the completed response. |
| `List the files in my project directory.` | The filesystem tools from the console demo are not exposed by this hosted agent. | The request does not gain file access; the agent may explain that its configured tool is only for greetings. |
| ` ` (empty or whitespace-only message) | The host rejects the request before connecting to the MCP server. | Input validation error; no MCP call is made. |

## Run locally

Prerequisites: .NET 10 SDK, azd with the `azure.ai.agents` extension, shared [Foundry user-secrets](../../../README.md#configure-foundry-user-secrets), and a reachable Streamable HTTP MCP server. Start the sample greeting server in a separate terminal from the repository root; it listens on the local `/mcp` endpoint and is not deployed as part of this project:

```powershell
dotnet run --project .\dotnet\src\03-KnowledgeAndAction\Chapter10.MCP.Server -- --urls http://localhost:8000
```

For local hosted-agent testing, point the host at that server. `localhost` is appropriate only while both processes run on the same machine:

```powershell
Set-Location .\dotnet\src\03-KnowledgeAndAction\Chapter10.MCP.Hosted
$env:CHAPTER10_MCP_SERVER_URL = 'http://localhost:8000/mcp'
azd ai agent run chapter10-mcp
# or run the Responses host directly:
dotnet run
```

In a second terminal, from the same hosted-project folder:

```powershell
$env:CHAPTER10_MCP_SERVER_URL = 'http://localhost:8000/mcp'
azd ai agent invoke chapter10-mcp --local --protocol responses 'Use the greet tool to greet Ada.'
azd ai agent invoke chapter10-mcp --local --protocol responses 'Please greet Morgan using the greeting tool.'
```

For direct `dotnet run`, configure the shared Foundry project/model settings as documented in the [shared user-secrets guide](../../../README.md#configure-foundry-user-secrets). Local model calls can use the configured API key or developer identity; hosted Azure model calls use the runtime managed identity, not a packaged API key. The Chapter 10 console MAF demo retains its separate shared user-secrets API-key path. Stop both processes with Ctrl+C. The Chapter 10 console greeting demo can also use this server with `dotnet run --project ..\Chapter10.MCP -- greet` (from this hosted-project folder); the console filesystem demo is a separate stdio child-process example and requires Node.js/`npx`.

## Deploy to Foundry

Follow the [shared hosted-agent guide](../../../README.md#configure-and-deploy-hosted-agents). The MCP server is separate from this service and must be hosted independently at an endpoint reachable by the Foundry host; a developer's `localhost` URL cannot be used for remote invocations. Run azd commands from this hosted project folder.

1. **Sign in and create an azd environment:**

   ```powershell
   Set-Location .\dotnet\src\03-KnowledgeAndAction\Chapter10.MCP.Hosted
   az login --tenant '<tenant-id>'
   azd auth login --tenant-id '<tenant-id>'
   azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
   azd env set AZURE_TENANT_ID '<tenant-id>'
   azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
   azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
   azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-model-deployment>'
   azd env set CHAPTER10_MCP_SERVER_URL 'https://<reachable-mcp-server>/mcp'
   ```

2. **Edit [`azure.yaml`](azure.yaml).** For more about this configuration, see [point the manifest at your project and model](../../../README.md#point-the-manifest-at-your-project-and-model). Replace the sample `mvp-playground-foundry-proj` project key and endpoint with your project and update `uses`. Change the service's `AZURE_AI_MODEL_DEPLOYMENT_NAME` value to `${AZURE_AI_MODEL_DEPLOYMENT_NAME}` so the azd environment value from step 1 supplies your chat deployment. Ensure `CHAPTER10_MCP_SERVER_URL` is the trusted, reachable HTTPS Streamable HTTP MCP endpoint. The sample `Chapter10.MCP.Server` has no `azure.yaml` and is not deployed by this command; host it separately before deploying the agent.

3. **Deploy:**

   ```powershell
   azd deploy chapter10-mcp --no-prompt
   ```

4. **Grant runtime model access.** Run `azd ai agent show chapter10-mcp --output json`, resolve the current `instance_identity.principal_id`, and check effective assignments including inherited grants. If needed, obtain authorization and grant **Cognitive Services OpenAI User** at the actual model-hosting Foundry account scope. Do not substitute the deploying user, project identity, or agent blueprint. Allow for RBAC propagation and recheck after redeployment. The MCP server's own authentication/authorization is separate and must be configured at that server boundary if required.

   ```powershell
   azd ai agent show chapter10-mcp --output json
   az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
   ```

5. **Invoke after access is available:**

   ```powershell
   azd ai agent invoke chapter10-mcp --protocol responses 'Use the greet tool to greet Ada.'
   ```

Bundled .NET 10 code deployment includes the hosted agent with its referenced Chapter 10 and Shared projects. No Docker image or container registry is required; the MCP server remains a separately managed dependency.

## MCP server hosting and trust boundary

The included `Chapter10.MCP.Server` exposes a stateless Streamable HTTP MCP endpoint at `/mcp` and a `greet` tool that validates a non-empty name and returns a greeting. Run it locally with the command above; to use it from a deployed agent, host it separately on a network reachable by Foundry and use its HTTPS `/mcp` URL. This sample server project has no `azure.yaml`, so it requires its own hosting/deployment approach. Configure server authentication and least-privilege access if needed; do not put endpoint credentials in source or traces. The agent's allowlist is fixed to `greet` even if the server exposes additional tools.

## Telemetry and sensitive data

The `Chapter10.MCP` activity source records the MCP agent and model/tool execution spans. **Sensitive-data capture is enabled for this demo:** prompts, tool inputs, tool descriptions, and results can appear in traces. Use non-sensitive requests and restrict telemetry access and retention; disable sensitive capture before confidential use. To view local spans, follow [View local traces with Aspire Dashboard](../../../README.md#view-local-traces-with-aspire-dashboard) with `<service>` set to `chapter10-mcp`.

## Costs and cleanup

Hosted compute and model inference incur Azure charges; the separately hosted MCP server may have its own costs. Remove only the reader-owned `chapter10-mcp` agent and versions through Foundry's agent-scoped controls. Manage the MCP server only through its own hosting provider and owner controls. Never run `azd down` against a shared Foundry project or remove shared resources.

## Pattern tradeoffs

MCP provides a reusable boundary between the agent and an independently hosted tool server, but introduces network availability, latency, trust, and server-authentication concerns. Runtime discovery does not make discovered tools safe; deterministic allowlists and server-side authorization remain important. The hosted example deliberately exposes one harmless tool and leaves the local filesystem demonstration outside the hosted request path.
