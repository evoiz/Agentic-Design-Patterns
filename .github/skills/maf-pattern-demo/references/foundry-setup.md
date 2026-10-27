# Foundry + Shared Solution Setup

Create once under `dotnet/`. Verify package names/versions against nuget.org and MAF docs at implementation time (preview APIs move).

## Layout
```
dotnet/
├── AgenticDesignPatterns.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── README.md
└── src/
    ├── Shared/Shared.csproj
    │   ├── FoundrySettings.cs
    │   └── FoundryChatClientFactory.cs
    └── 01-Flow/                       # one folder per pattern family (see docs/design-patterns.md)
        └── Chapter01.PromptChaining/...
```

Tests mirror the family folders under `dotnet/tests/<Family>/`.

## Directory.Build.props (key parts)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <!-- One secrets.json shared by every chapter -->
    <UserSecretsId>agentic-design-patterns-maf</UserSecretsId>
  </PropertyGroup>
</Project>
```

## Packages (central versions, use latest prerelease where noted)

Core (MAF + model deployment, always):
| Package | Why |
|---|---|
| `Microsoft.Agents.AI` (prerelease) | `AIAgent`, `ChatClientAgent` |
| `Microsoft.Agents.AI.OpenAI` (prerelease) | OpenAI-compatible chat client → agent |
| `Microsoft.Agents.AI.Workflows` (prerelease) | `WorkflowBuilder`, `AgentWorkflowBuilder`, executors |
| `OpenAI` | `OpenAIClient` with `ApiKeyCredential` |
| `Microsoft.Extensions.AI.OpenAI` (prerelease) | `AsIChatClient()` |
| `Microsoft.Extensions.Configuration.UserSecrets` | read `secrets.json` |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | CI override |
| `Microsoft.Extensions.Configuration.Binder` | bind `FoundrySettings` |

Foundry-native variants only (add to the chapter project, not Shared core):
| Package | Why |
|---|---|
| `Azure.AI.Projects` (prerelease) | `AIProjectClient`, Agent Service / hosted agents |
| `Microsoft.Agents.AI.Foundry` (prerelease) | Foundry agent → `AIAgent` |
| `Azure.Identity` | Agent Service APIs require Entra ID (`az login`) |

## Local secrets.json keys
```json
{
  "Foundry:ProjectEndpoint": "https://<resource>.services.ai.azure.com/api/projects/<project>",
  "Foundry:ApiKey": "<key from Foundry portal>",
  "Foundry:ModelDeployment": "gpt-4.1-mini",
  "Foundry:SmallModelDeployment": "<optional, for routing/SLM demos>"
}
```
Set via (user types the key; agent never asks for it in chat):
```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "<endpoint>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ApiKey" "<key>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<deployment>" --project dotnet/src/Shared
```
Env var override: `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, `Foundry__ModelDeployment`.

## Cloud-agent / GitHub Actions setup

These API-key settings apply to console demos. Separate hosted companions use local developer credentials and Azure managed identity; do not upload local user-secrets or require an API key inside the hosted runtime.

For cloud-agent validation, do not rely on local user-secrets. Instead:

1. Create a protected GitHub Environment named `foundry-integration`.
2. Add environment secrets:
   - `FOUNDRY_PROJECT_ENDPOINT`
   - `FOUNDRY_API_KEY`
   - `FOUNDRY_MODEL_DEPLOYMENT`
   - optional `FOUNDRY_MODEL_DEPLOYMENT_FAST`
   - optional `FOUNDRY_MODEL_DEPLOYMENT_QUALITY`
3. Use `.github/workflows/live-foundry-tests.yml` as the manual smoke-test entry point.
4. In that workflow, map the environment secrets to:
   - `Foundry__ProjectEndpoint`
   - `Foundry__ApiKey`
   - `Foundry__ModelDeployment`
   - optional `Foundry__ModelDeployments__fast`
   - optional `Foundry__ModelDeployments__quality`

This keeps credentials out of the repository while allowing the cloud agent and GitHub-hosted runners to validate new implementations against a live Foundry deployment.

## Shared code sketch
The model is called through the resource's OpenAI-compatible v1 endpoint, derived from the project endpoint host: `https://<resource>.services.ai.azure.com/openai/v1/`.
```csharp
public sealed record FoundrySettings
{
    public required string ProjectEndpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string ModelDeployment { get; init; }
    public string? SmallModelDeployment { get; init; }

    public Uri OpenAIEndpoint => new($"https://{new Uri(ProjectEndpoint).Host}/openai/v1/");
}

public static class FoundryChatClientFactory
{
    public static FoundrySettings LoadSettings()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
            .AddEnvironmentVariables()
            .Build();

        return config.GetSection("Foundry").Get<FoundrySettings>()
            ?? throw new InvalidOperationException(
                "Foundry settings missing. See dotnet/README.md to configure user-secrets or Foundry__* environment variables.");
    }

    public static IChatClient CreateChatClient(FoundrySettings s, string? deployment = null) =>
        new OpenAIClient(new ApiKeyCredential(s.ApiKey), new OpenAIClientOptions { Endpoint = s.OpenAIEndpoint })
            .GetChatClient(deployment ?? s.ModelDeployment)
            .AsIChatClient();
}
```
`AddUserSecrets(assembly)` reads the `UserSecretsId` attribute — since it is set in `Directory.Build.props`, every project resolves the same `secrets.json`. `AddEnvironmentVariables()` lets GitHub Actions override the same settings during cloud-agent runs.

## Hosted deployment companions

At the end of a chapter implementation, follow SKILL.md's hosted-companion suitability step and load `microsoft-foundry` for current scaffolding/deployment guidance. Use a separate `Chapter<NN>.<PatternName>.Hosted` project referencing shared chapter logic. Hosting a MAF workflow is distinct from a Foundry-native pattern implementation.

Verify the packaging boundary: a host source folder alone does not include sibling project references or `Directory.Build.props` / `Directory.Packages.props`. Use a complete remote-build context or bundled dependencies. For .NET bundled deployment, the current azd agent extension runs `dotnet publish` locally and ZIPs its output; keep the service's `project` pointing to the source project, not an already-published DLL folder. Chapter 02 provides a bundled reference through `dotnet/src/01-Flow/Chapter02.Routing.Hosted/azure.yaml`; each hosted project has its own manifest. Verify this behavior against the installed extension rather than duplicating its packaging logic.

Keep azd state and published artifacts ignored, configure the project/model explicitly, use managed identity in Azure, and document required inference/deployment permissions. Adding deployment artifacts does not authorize provisioning or deployment. After an authorized deployment, verify the active version and a remote protocol invocation; console smoke tests alone are insufficient.

### Runtime inference RBAC (required for hosted deployment)

A hosted version can report `active` but fail its first model call with `401 PermissionDenied: Principal does not have access to API/Operation.` Deployment permissions and runtime model-inference permissions are separate. Make the following part of every authorized hosted deployment, before the remote smoke test:

1. Include assignment of **Cognitive Services OpenAI User** in the deployment approval. If approval did not cover RBAC writes, ask before creating the assignment. This skill update is not itself permission to change Azure resources.
2. From the selected azd project/environment, run `azd ai agent show <service> --output json` and read `instance_identity.principal_id`. Resolve the actual model-hosting Foundry account ARM ID through read-only Azure discovery; do not guess it from the agent name or assume the project hosts every model. Use the selected subscription and tenant explicitly. Do not use the deploying user, project managed identity, or `blueprint.principal_id` instead of the runtime identity.
3. Check role assignments at that account scope, including inherited assignments. Use `--fill-principal-name false` to avoid unnecessary Microsoft Graph lookups. Reuse existing Cognitive Services OpenAI User or verified equivalent inference access instead of creating duplicate or redundant grants.
4. When missing and approved, assign the role to the runtime principal at the model-hosting **account** scope. Use its object ID and `ServicePrincipal` type so no directory lookup is required. Do not grant Contributor/Owner or use resource-group/subscription scope as a workaround.

   ```powershell
   # Set these from successful discovery for the selected deployment, not sample IDs.
   $subscriptionId = '<model-hosting-subscription-id>'
   $accountId = '<discovered-model-hosting-account-arm-id>'
   $runtimePrincipalId = '<instance_identity.principal_id>'

   az role assignment list --subscription $subscriptionId --scope $accountId --include-inherited --fill-principal-name false --query "[?principalId=='$runtimePrincipalId'].{role:roleDefinitionName,roleDefinitionId:roleDefinitionId,scope:scope}" --output json

   # Run only when the check above shows missing inference access and RBAC writes are approved.
   az role assignment create --subscription $subscriptionId --assignee-object-id $runtimePrincipalId --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope $accountId
   ```

5. Verify the assignment and allow for RBAC propagation before invoking. If the first call still returns `401 PermissionDenied`, confirm the principal and model endpoint, wait and retry with a fresh hosted session using the installed CLI's supported flags. Keep retries bounded; if access remains denied, report the blocker rather than repeatedly deploying new versions or switching to API keys. Missing role-assignment privileges require an administrator, not broader grants.
6. Re-resolve the runtime principal after each redeployment and repeat the check, since a new version may use a different identity. Record the role/scope and remote invocation outcome in the hosted README; do not claim readiness based only on deployment or role-assignment success.

## Agent creation pattern
```csharp
var settings = FoundryChatClientFactory.LoadSettings();
IChatClient chat = FoundryChatClientFactory.CreateChatClient(settings);

ChatClientAgent extractor = new(chat,
    name: "Extractor",
    instructions: "Extract the technical specifications from the text.");
```

## Agent run pattern (non-streaming, preferred)
```csharp
AgentResponse response = await extractor.RunAsync(inputText);
Console.WriteLine(response.Text);

// Structured output
AgentResponse<Specs> specs = await transformer.RunAsync<Specs>(response.Text);
```

## Workflow run pattern (non-streaming, preferred)
```csharp
var workflow = AgentWorkflowBuilder.BuildSequential([agentA, agentB]);
Run run = await InProcessExecution.RunAsync(workflow, messages);
foreach (WorkflowEvent evt in run.NewEvents)
{
    if (evt is WorkflowOutputEvent output)
        Console.WriteLine(output.Data);
}
```
Verify exact type/member names (`Run`, `NewEvents`, `AgentResponse`) against current MAF docs/build — preview APIs change.

## Workflow run pattern (streaming — only when required, e.g. HITL)
```csharp
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
await foreach (WorkflowEvent evt in run.WatchStreamAsync()) { /* RequestInfoEvent / WorkflowOutputEvent */ }
```
