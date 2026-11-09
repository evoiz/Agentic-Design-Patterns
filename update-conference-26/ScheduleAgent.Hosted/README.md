# Deploy the custom schedule agent from this project

This is a custom **C# Hosted Agent**, not a Foundry Prompt Agent. It exposes the same [local schedule RAG](../ConferenceDemos/Demos/03-ScheduleRag/ScheduleDemo.cs) through Foundry's Responses protocol.

## How this project was created

The project files were written directly, adapting the repository's [Chapter 14 hosted entry point](../../dotnet/src/RAG.Hosted/Program.cs) and its `azure.yaml`. No `azd init`, Foundry scaffolding service, portal agent wizard, or special agent-generation tool created this project. The project uses the ordinary `Microsoft.NET.Sdk.Web` SDK shape (the same base as `dotnet new web`), but **the template command was not run**.

The important agent-specific wiring is explicit:

1. A project reference shares the conference behavior and its embedded schedule.
2. `ScheduleHostedAgent` extends the existing `Shared.Hosting.StatelessWorkflowAgent`, propagating caller input, cancellation, and errors.
3. `Program.cs` uses the official pinned `Microsoft.Agents.AI.Foundry.Hosting` package: `AgentHost.CreateBuilder`, `AddFoundryResponses`, and `MapFoundryResponses`.
4. `azure.yaml` was adapted to this project's DLL, service, and parameterized target; azd performs bundled .NET publishing and code deployment. It does not generate the business agent.

The `maf-pattern-demo` skill supplied repository conventions. The actual agent and protocol integration are ordinary C# SDK code; azd is the deployment tool, not an agent-code generator. The conference solution file was generated with `dotnet new sln`, and projects were added with `dotnet sln add`.

## Request, credentials, and state

Send a plain-text question, up to 4,000 characters, such as `What should I attend today if I'm interested in AI and .NET?`. Each request gets fresh agent/context-provider state and a two-minute deadline. MAF's built-in `TextSearchProvider` injects the complete embedded schedule before one text-generation call; there is no keyword search, retrieval tool loop, or JSON answer contract. The host always uses **conference day 1 (November 12, CET)**; the local console additionally supports `--day 2`. Both days are in context, with the selected day specified in instructions. Recommendations, citations, and absence-of-evidence answers are model-generated, not deterministically validated.

The latest user message is the request; prior conversation messages do not provide business memory. The adapter buffers the completed answer for protocol streaming. No durable conversation store is part of the demo's business logic; the protocol server may retain response artifacts independently.

The host reads environment/command-line configuration, not local user-secrets, and explicitly ignores `Foundry:ApiKey`. Local host inference uses `DefaultAzureCredential` (for example Azure CLI login); Azure execution uses its runtime managed identity. Console demos retain their existing shared API-key path.

## 1. Prepare tools and an existing Foundry project

Install .NET 10, Azure CLI, Azure Developer CLI **>=1.35.1**, and its `azure.ai.agents` extension **>=1.0.0-beta.19**, as required by [azure.yaml](azure.yaml). Check your installed CLI's extension help if installation is needed:

```powershell
dotnet --version
az version
azd version
azd extension list
azd extension install azure.ai.agents
azd ai agent --help
```

Choose a reader-owned existing Foundry project with hosted-agent availability/capacity in its region and a deployed chat model with enough context for the complete schedule (about 75 KB). This agent does not require tools or structured output. Prepare these before presenting; do not provision projects/models live. Hosted execution, inference, and telemetry can incur charges.

You need permission to deploy agents to that project, local model-inference access, and an authorized administrator able to grant runtime inference access at the **actual model-hosting account**. Deployment access is not inference access; do not solve missing rights with broader subscription roles.

From the repository root:

```powershell
Set-Location update-conference-26/ScheduleAgent.Hosted
az login --tenant '<tenant-id>'
az account set --subscription '<subscription-id>'
azd auth login --tenant-id '<tenant-id>'
```

All remaining commands run **from this project directory**, not the repository root. Discover existing IDs instead of constructing them:

```powershell
az cognitiveservices account show --resource-group '<resource-group>' --name '<account>' --query id --output tsv
az resource list --resource-type Microsoft.CognitiveServices/accounts/projects --query "[].{name:name,id:id}" --output table
```

Select the exact project ARM ID and endpoint from your target's resource details/portal. If inference uses another account, discover that account's ARM ID separately for the RBAC step.

## 2. Verify local host and bundled data

```powershell
$env:Foundry__ProjectEndpoint = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:Foundry__ModelDeployment = '<chat-deployment>'
$env:AZURE_TENANT_ID = '<tenant-id>'
dotnet build ScheduleAgent.Hosted.csproj
dotnet run --project ScheduleAgent.Hosted.csproj
```

Use the listening URL printed at startup (the pinned hosting stack normally uses port 8088). In another terminal:

```powershell
$body = @{
    model = 'update-schedule'
    input = "What should I attend today if I'm interested in AI and .NET?"
    stream = $false
    store = $false
} | ConvertTo-Json
Invoke-RestMethod -Uri 'http://localhost:8088/responses' -Method Post -ContentType 'application/json' -Body $body
```

Inspect the final output for real schedule titles/times and `#s...` references. Stop the host with Ctrl+C. To check the packaging boundary without Azure credentials:

```powershell
dotnet test ../ConferenceDemos.Tests/ConferenceDemos.Tests.csproj
dotnet publish ScheduleAgent.Hosted.csproj -c Release -o publish
```

The tests cover the embedded canonical fixture and hosted request behavior. Check that `publish` contains `ScheduleAgent.Hosted.dll`, `ConferenceDemos.dll`, `AgenticDesignPatterns.Shared.dll`, `AgenticDesignPatterns.Shared.Hosting.dll`, the host `.deps.json`, and `.runtimeconfig.json`. Keep the repository checkout intact: source project references cross back into `dotnet/`. Do not upload just this source folder or point azd's `project` at `publish`.

## 3. Configure an azd environment here

```powershell
azd env new '<conference-environment>'
# For subsequent sessions: azd env select '<conference-environment>'
azd env set AZURE_TENANT_ID '<tenant-id>'
azd env set AZURE_SUBSCRIPTION_ID '<subscription-id>'
azd env set AZURE_AI_PROJECT_ID '<discovered-project-arm-id>'
azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-deployment>'
azd env set ENABLE_SENSITIVE_TELEMETRY 'false'
```

`conference-project.endpoint` in `azure.yaml` references `${AZURE_AI_PROJECT_ENDPOINT}`; there is no hardcoded account to edit. `project: .`, `dependencyResolution: bundled`, and `entryPoint: ScheduleAgent.Hosted.dll` tell the extension to publish locally, including referenced projects and NuGet dependencies, before upload. The schedule is embedded in `ConferenceDemos.dll`; it is not a separate path that the remote container must resolve.

`.agentignore` excludes local state, credentials, captures, and source build intermediates. Do not copy API keys into azd environment values or deploy user-secrets. Shared source build/package imports must stay available for the local publish step.

## 4. Deploy only this service

```powershell
azd deploy update-schedule --no-prompt
azd ai agent show update-schedule --output json
```

This command creates/updates the selected agent code deployment in the existing project. Do not run `azd up` or provision unrelated infrastructure for this demo. Read errors rather than falling back to another project or region. Use the installed extension's help if a newer version changes flags.

## 5. Verify the current runtime identity's inference access

From the show output, take **`instance_identity.principal_id`**, not `blueprint.principal_id`, the project identity, or your user identity. Also note the deployed version/state. Resolve the real model-hosting account scope from step 1.

```powershell
$runtimePrincipalId = '<instance_identity.principal_id>'
$modelAccountId = '<discovered-model-hosting-account-arm-id>'
$modelSubscriptionId = '<model-hosting-subscription-id>'
az role assignment list --subscription $modelSubscriptionId --scope $modelAccountId --include-inherited --fill-principal-name false --query "[?principalId=='$runtimePrincipalId'].{role:roleDefinitionName,id:roleDefinitionId,scope:scope}" --output table
```

If existing effective access already permits inference, reuse it. **Only when missing and authorized**, have an administrator run:

```powershell
az role assignment create --subscription $modelSubscriptionId --assignee-object-id $runtimePrincipalId --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope $modelAccountId
```

That role is **Cognitive Services OpenAI User**, scoped to the model-hosting account. Re-run the check, allow RBAC propagation, then invoke. A `401 PermissionDenied` can mean missing runtime access even when deployment is active. Check principal and endpoint, wait, and retry a bounded number of times; do not repeatedly redeploy, broaden roles, or insert API keys. Re-resolve the runtime principal after every redeployment because it may change.

## 6. Invoke remotely, then verify in the portal

```powershell
azd ai agent invoke update-schedule --protocol responses "What should I attend today if I'm interested in AI and .NET?"
```

In the Foundry portal, open the same project, go to Agents, select **Update Conference Schedule / update-schedule** and its active hosted version, and open the available agent playground/test view. Send the same question. Confirm the final answer references real fixture sessions, times, and source IDs without dumping the schedule. Also ask `unicorn astrophysics` and confirm the model explains that the schedule has no relevant session.

An active version or successful upload alone is not acceptance: obtain an actual response **in the portal** during rehearsal and prepare a short capture. If your portal/project does not offer hosted invocation, resolve that prerequisite before the talk; CLI invocation is a diagnostic, not a substitute for the portal acceptance gate.

## 7. Tracing, presentation, and cleanup

`Program.cs` registers the `UpdateConference.Schedule` OpenTelemetry source with Foundry's host tracing. For a rehearsal using **only public/synthetic input**, opt into sensitive traces and deploy the selected service again:

```powershell
azd env set ENABLE_SENSITIVE_TELEMETRY 'true'
azd deploy update-schedule --no-prompt
```

Sensitive traces can include questions, injected schedule context, and responses. Restrict access/retention and set the flag back to `false` before handling private input. Recheck the new runtime identity's inference access after redeploying. Trace export requires the host/environment's configured telemetry destination; console final-answer filtering does not redact telemetry.

Timebox onstage deployment/invocation; switch to a prepared capture and the previously deployed version instead of changing permissions or provisioning infrastructure live. Do not automatically start evaluation suites; they are separate, billable work requiring a dataset and an explicit decision.

When finished, stop/delete only your `update-schedule` agent versions via the project's agent controls (or the installed CLI's documented agent-scoped commands). Remove only RBAC grants created solely for a retired identity when authorized. **Do not run `azd down` against a shared project**, delete shared models/accounts, or remove unrelated agents. See the [common hosting guide](../../dotnet/HOSTING.md) for broader operational context.
