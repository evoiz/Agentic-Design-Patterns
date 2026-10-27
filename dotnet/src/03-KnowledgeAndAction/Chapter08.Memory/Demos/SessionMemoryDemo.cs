using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter08.Memory.Demos;

public static class SessionMemoryDemo
{
    public static async Task RunAsync(
        IChatClient chatClient,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        cancellationToken.ThrowIfCancellationRequested();

        var memoryProvider = new ConversationMemoryProvider();
        AIAgent agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "SessionMemoryAssistant",
                ChatOptions = new()
                {
                    Instructions = """
                        You are a friendly travel assistant. Use the conversation and explicitly shared facts
                        to answer follow-up questions. Do not invent personal details.
                        """
                },
                AIContextProviders = [memoryProvider]
            });

        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        writeOutput?.Invoke("--- Explicit session-state update ---");
        writeOutput?.Invoke(memoryProvider.LogUserLogin(session));
        writeOutput?.Invoke($"Task status: {memoryProvider.GetState(session).TaskStatus}");
        await RunTurnAsync(agent, session, memoryProvider, "My name is Sam.", "First turn", writeOutput, cancellationToken);
        await RunTurnAsync(
            agent,
            session,
            memoryProvider,
            "I prefer window seats when I travel.",
            "Explicit memory update",
            writeOutput,
            cancellationToken);

        writeOutput?.Invoke("\n--- Recall from the same session ---");
        await RunTurnAsync(
            agent,
            session,
            memoryProvider,
            "What is my name, and what kind of seat do I prefer?",
            "Recall",
            writeOutput,
            cancellationToken);

        writeOutput?.Invoke("\n--- Serialize and restore the session ---");
        var serializedSession = await agent.SerializeSessionAsync(session);
        AgentSession restoredSession = await agent.DeserializeSessionAsync(serializedSession);
        await RunTurnAsync(
            agent,
            restoredSession,
            memoryProvider,
            "What travel preference did I share?",
            "Restored session",
            writeOutput,
            cancellationToken);
    }

    private static async Task RunTurnAsync(
        AIAgent agent,
        AgentSession session,
        ConversationMemoryProvider memoryProvider,
        string input,
        string heading,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        writeOutput?.Invoke($"\n--- {heading} ---");
        writeOutput?.Invoke($"User: {input}");
        var response = await agent.RunAsync(input, session, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException($"The memory agent returned an empty response during '{heading}'.");
        }

        writeOutput?.Invoke($"Assistant: {response.Text}");
        var state = memoryProvider.GetState(session);
        if (state.Facts.Count > 0)
        {
            writeOutput?.Invoke("Session facts:");
            foreach (var fact in state.Facts.OrderBy(fact => fact.Key, StringComparer.OrdinalIgnoreCase))
            {
                writeOutput?.Invoke($"- {fact.Key}: {fact.Value}");
            }
        }

        if (!string.IsNullOrWhiteSpace(state.LastResponse))
        {
            writeOutput?.Invoke("Stored output key: last_response");
        }
    }
}
