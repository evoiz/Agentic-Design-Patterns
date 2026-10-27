using A2A;
using Microsoft.Agents.AI;

namespace Chapter15.A2A;

public static class A2AClientDemo
{
    public static async Task RunAsync(
        string mode,
        Uri agentUrl,
        string? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(agentUrl);
        if (agentUrl.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The A2A agent URL must use HTTP or HTTPS.", nameof(agentUrl));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var resolver = new A2ACardResolver(agentUrl);
        AgentCard card = await resolver.GetAgentCardAsync(cancellationToken);
        PrintCard(card);

        if (mode.Equals("card", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (mode is "sync" or "all")
        {
            var message = request ?? "What is the exchange rate from USD to EUR?";
            Console.WriteLine("\n=== Synchronous A2A request (JSON-RPC) ===");
            AIAgent jsonRpcAgent = card.AsAIAgent(options: new A2AClientOptions
            {
                PreferredBindings = [ProtocolBindingNames.JsonRpc]
            });
            AgentSession session = await jsonRpcAgent.CreateSessionAsync(cancellationToken);
            AgentResponse response = await jsonRpcAgent.RunAsync(
                message,
                session,
                cancellationToken: cancellationToken);
            Console.WriteLine(response.Text);
        }

        if (mode is "stream" or "all")
        {
            var message = request ?? "What's the exchange rate for JPY to GBP today?";
            Console.WriteLine("\n=== Streaming A2A request (HTTP+JSON) ===");
            AIAgent httpJsonAgent = card.AsAIAgent(options: new A2AClientOptions
            {
                PreferredBindings = [ProtocolBindingNames.HttpJson]
            });
            AgentSession session = await httpJsonAgent.CreateSessionAsync(cancellationToken);
            await foreach (var update in httpJsonAgent.RunStreamingAsync(
                               message,
                               session,
                               cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    Console.Write(update.Text);
                }
            }

            Console.WriteLine();
        }
    }

    private static void PrintCard(AgentCard card)
    {
        Console.WriteLine("=== Discovered A2A agent card ===");
        Console.WriteLine($"Name: {card.Name}");
        Console.WriteLine($"Description: {card.Description}");
        foreach (var skill in card.Skills)
        {
            Console.WriteLine($"Skill: {skill.Name} — {skill.Description}");
        }
    }
}
