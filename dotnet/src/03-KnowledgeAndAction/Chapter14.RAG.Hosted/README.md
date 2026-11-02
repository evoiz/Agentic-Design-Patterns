# Chapter 14 — Hosted RAG Agent

Exposes the [Chapter 14 local-corpus RAG agent](../Chapter14.RAG/README.md) through the Foundry **Responses protocol**. Common build, deploy, inference RBAC, and cleanup steps are in the [hosting guide](../../../HOSTING.md).

## Where to look

- `RagHostedAgent.RunAsync` — passes the caller's question to `RagAgent.RunAsync` via the shared `StatelessWorkflowAgent`.
- `Program.cs` — shared `HostedFoundryConfiguration.CreateChatClient` and `Chapter14.RAG` tracing.

## Request contract

Send a plain-text question, e.g. `What did the president say about Justice Breyer?`. Each request searches the bundled sample corpus and returns one buffered answer with `[SOURCE-ID]` citations, or states that the documents do not contain the answer. Requests are stateless; no conversation is persisted. File Search and Web Search remain console-only variants; the host provisions no vector store or search connection.

## Run locally and deploy

```powershell
dotnet run --project dotnet/src/03-KnowledgeAndAction/Chapter14.RAG.Hosted
azd ai agent invoke chapter14-rag --local --protocol responses "What did the address say about the economy?"
azd deploy chapter14-rag --no-prompt
azd ai agent invoke chapter14-rag --protocol responses "What did the president say about Justice Breyer?"
```

Run `azd` commands from this project's directory, which contains its `azure.yaml`. Grant the runtime identity inference access before remote invocation (see the hosting guide).

## Telemetry

Spans under `Chapter14.RAG` capture sensitive data (questions, retrieved passages, answers). Use non-sensitive inputs and restrict trace access.
