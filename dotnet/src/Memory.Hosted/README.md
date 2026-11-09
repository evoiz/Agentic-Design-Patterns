# Hosted Foundry Memory

Exposes the [Chapter 08 Foundry-managed memory demo](../Memory/README.md) through the Foundry Responses protocol (`MemoryDemoAgent` derives from the shared `StatelessWorkflowAgent`). Common build, deploy, RBAC, and cleanup steps are in the [hosting guide](../../HOSTING.md).

## Request contract

Send a recall question, for example `What seat do I prefer?`. Each request:

1. creates a **fresh temporary memory scope** (a configured `Foundry:MemoryScope` is ignored);
2. stores the synthetic fact "Sam prefers window seats" and waits for memory extraction;
3. asks **your message** in a new MAF session, so the answer can only come from managed memory;
4. deletes the scope's memories after a successful run and returns the full transcript.

This is a pattern demonstration, not a personal-memory chatbot: memory is never shared between callers or invocations. An interrupted run can leave synthetic memories in its isolated scope; rely on store retention or administrative deletion. A personal-memory agent must first map authenticated callers to authorized durable scopes and define consent, retention, and deletion.

## Configuration

| Variable | Purpose |
|---|---|
| `Foundry__ProjectEndpoint` / `FOUNDRY_PROJECT_ENDPOINT` | Project endpoint |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Chat deployment |
| `AZURE_AI_EMBEDDING_DEPLOYMENT_NAME` | Embedding deployment |
| `AZURE_AI_MEMORY_STORE_ID` | Reader-owned dedicated memory-store name (created if absent) |

## Run and deploy

Set the project/chat/embedding/store variables in the selected azd environment first;
the reusable manifest interpolates them rather than using a maintainer's resource.

```powershell
azd ai agent run chapter08-memory --no-client
azd ai agent invoke chapter08-memory --local "What seat do I prefer?"
azd deploy chapter08-memory --no-prompt
azd ai agent invoke chapter08-memory "What seat do I prefer?"
```

**Chapter-specific RBAC:** besides model inference, the runtime `instance_identity.principal_id` needs **Foundry User** (`53ca6127-db72-4b80-b1b0-d745d6d5456d`) at the Foundry account scope for memory-store access — see [memory authorization](https://learn.microsoft.com/azure/foundry/agents/how-to/memory-usage#authorization-and-permissions). This role is account-wide; use an account appropriate for the demo. Remote calls take longer because memory extraction is asynchronous.
