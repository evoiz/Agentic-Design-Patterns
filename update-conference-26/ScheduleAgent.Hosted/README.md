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

Send a plain-text question, up to 4,000 characters, such as `What should I attend today if I'm interested in AI and .NET?`. Each request gets fresh state and a two-minute deadline. Direct questions shaped like `Is <speaker> speaking on [conference] day 1/2?` or `Is <speaker> speaking on November 12/13[, 2026]?` use deterministic lookup over the embedded session records, with case/accent-insensitive, whole-name-token matching. Ambiguous names request the full speaker name. This path makes no model call and reports only appearances listed in the snapshot.

For other questions, MAF's built-in `TextSearchProvider` injects the complete embedded schedule plus a compact speaker/day index derived from all session records before one text-generation call; there is no keyword search, retrieval tool loop, or JSON answer contract.

Both days are available locally and in Foundry. Specify the day in the question, for example `Is Cédric speaking on day 2?`; there is no separate hosted day parameter or local RAG `--day` option. Only **"today" defaults to day 1 (November 12, CET)**; an explicit day/date takes precedence, and questions without a day constraint search both days. Instructions require speaker and day to match the same session: a Day 1 appearance cannot justify "yes" for Day 2. In this snapshot Cédric's session is **#s13 on Day 1**, so the Day 2 question should receive a negative answer, optionally followed by the clearly labeled Day 1 correction.

Direct speaker/day lookup answers and their citations are constructed from session records. Other factual answers, recommendations, citations, and absence-of-evidence answers remain model-generated, not deterministically validated. Tests verify direct speaker/day answers without a model, and verify that broader questions and the complete speaker/day evidence reach the model; scripted replies do not prove live semantic accuracy.

The latest user message is the request; prior conversation messages do not provide business memory. The adapter buffers the completed answer for protocol streaming. No durable conversation store is part of the demo's business logic; the protocol server may retain response artifacts independently.

The host reads shared local user-secrets, then this project's user-secrets, then environment variables and command-line configuration (later sources take precedence). It explicitly ignores `Foundry:ApiKey`, including keys loaded from user-secrets. Inference uses the Foundry project's `/openai/v1/` endpoint with the `https://ai.azure.com/.default` token scope, rather than bypassing the project via the account-level OpenAI endpoint. Local inference uses `DefaultAzureCredential` (for example Azure CLI login); Azure execution uses its runtime agent identity. Verify that identity's chat-completions access as described below; do not assume project access alone covers this operation. `azure.yaml` passes the full project endpoint explicitly. Console demos retain their existing shared API-key path. Missing or empty `EnableSensitiveTelemetry` defaults to `false`; invalid nonempty values fail explicitly.

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

You need permission to deploy agents to that project and local project model-inference access. Hosted execution uses the agent's built-in project access; the project identity must be able to access its model-hosting account. Deployment access is not inference access; do not solve missing rights with broader subscription roles.

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

Select the exact project ARM ID and full endpoint (`https://<account>.services.ai.azure.com/api/projects/<project>`) from your target's resource details/portal.

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

If direct local startup reports missing Foundry settings, configure the endpoint and model in shared user-secrets as described in the [shared setup](../../dotnet/README.md), or supply the environment variables above. `azd env` values are loaded by `azd ai agent run --no-client`, not by a direct `dotnet run`. If remote invocation reports `session_not_ready`, inspect the session logs: an empty `ENABLE_SENSITIVE_TELEMETRY` previously caused a Boolean-conversion exception before readiness. Keep the explicit `false` setting below even though an empty value is now handled safely.

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

## 5. Verify project-scoped inference access

From the show output, note the deployed version/state and **`instance_identity.principal_id`**, not `blueprint.principal_id` or your user identity. Confirm that `Foundry__ProjectEndpoint` contains the complete selected project endpoint and that `AZURE_AI_MODEL_DEPLOYMENT_NAME` names a deployment available to that project.

The host calls the project's OpenAI-compatible **chat-completions** endpoint using an `ai.azure.com` token. Do not assume the documented implicit project inference access covers every operation: this route can explicitly require `Microsoft.CognitiveServices/accounts/OpenAI/deployments/chat/completions/action` for the runtime agent identity.

If that exact action is denied, discover the actual model-hosting account's ARM ID and check the runtime principal's effective roles there. With administrator authorization, assign **Cognitive Services OpenAI User** at that account scope:

```powershell
$runtimePrincipalId = '<instance_identity.principal_id>'
$modelAccountId = '<discovered-model-hosting-account-arm-id>'
$modelSubscriptionId = '<model-hosting-subscription-id>'
az role assignment list --subscription $modelSubscriptionId --scope $modelAccountId --include-inherited --fill-principal-name false --query "[?principalId=='$runtimePrincipalId'].{role:roleDefinitionName,scope:scope}" --output table
# Only when missing and authorized:
az role assignment create --subscription $modelSubscriptionId --assignee-object-id $runtimePrincipalId --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope $modelAccountId
```

Allow RBAC propagation, then invoke from a fresh session and from the portal. Retain the required grant while the agent uses this operation: a successful call immediately after removing a role can reflect cached authorization and does not prove that the role is unnecessary. Recheck the runtime identity after redeployment.

For other `401 PermissionDenied` errors, check the endpoint, token audience, runtime identity, and model access against Microsoft's [hosted-agent permissions reference](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agent-permissions). Additional RBAC changes require administrator authorization. Never substitute an API key, broaden subscription permissions, or silently change the target project.

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

Sensitive traces can include questions, injected schedule context, and responses. Restrict access/retention and set the flag back to `false` before handling private input. Recheck project-scoped inference after redeploying. Trace export requires the host/environment's configured telemetry destination; console final-answer filtering does not redact telemetry.

Timebox onstage deployment/invocation; switch to a prepared capture and the previously deployed version instead of changing permissions or provisioning infrastructure live. Do not automatically start evaluation suites; they are separate, billable work requiring a dataset and an explicit decision.

When finished, stop/delete only your `update-schedule` agent versions via the project's agent controls (or the installed CLI's documented agent-scoped commands). Remove only RBAC grants created solely for a retired identity when authorized. **Do not run `azd down` against a shared project**, delete shared models/accounts, or remove unrelated agents. See the [common hosting guide](../../dotnet/HOSTING.md) for broader operational context.
