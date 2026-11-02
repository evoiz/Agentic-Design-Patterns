using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter08.Memory.Demos;

public static class SessionMemoryDemo
{
    // Scripted turns; Fact is the explicit state update applied after the turn.
    private static readonly (string Heading, string Input, (string Key, string Value)? Fact)[] Turns =
    [
        ("First turn", "My name is Sam.", ("name", "Sam")),
        ("Explicit memory update", "I prefer window seats when I travel.", ("travel preference", "window seats")),
        ("Recall from the same session", "What is my name, and what kind of seat do I prefer?", null)
    ];

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
        foreach (var (heading, input, fact) in Turns)
        {
            await RunTurnAsync(agent, session, memoryProvider, input, heading, writeOutput, cancellationToken);
            if (fact is { } update)
            {
                memoryProvider.Remember(session, update.Key, update.Value);
                writeOutput?.Invoke($"Explicit state update: {update.Key} = {update.Value}");
            }
        }

        writeOutput?.Invoke("\n--- Serialize and restore the session ---");
        var serializedSession = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
        AgentSession restoredSession = await agent.DeserializeSessionAsync(
            serializedSession,
            cancellationToken: cancellationToken);
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
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException($"The memory agent returned an empty response during '{heading}'.");
        }

        writeOutput?.Invoke($"Assistant: {response.Text}");
        var state = memoryProvider.GetState(session);
        foreach (var fact in state.Facts.OrderBy(fact => fact.Key, StringComparer.OrdinalIgnoreCase))
        {
            writeOutput?.Invoke($"Session fact: {fact.Key} = {fact.Value}");
        }

        if (!string.IsNullOrWhiteSpace(state.LastResponse))
        {
            writeOutput?.Invoke("Stored output key: last_response");
        }
    }
}
