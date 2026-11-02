using A2A;

var mode = args.ElementAtOrDefault(0) ?? "sync";
if (mode is not ("card" or "sync" or "stream" or "all"))
{
    throw new ArgumentException("Choose a mode: card, sync, stream, or all.");
}

var agentUrlValue = args.ElementAtOrDefault(1)
    ?? Environment.GetEnvironmentVariable("A2A_AGENT_URL")
    ?? "http://localhost:5090/";
if (!Uri.TryCreate(agentUrlValue, UriKind.Absolute, out var agentUrl))
{
    throw new ArgumentException("Provide an absolute A2A agent URL.", nameof(agentUrlValue));
}

if (agentUrl.Scheme is not ("http" or "https"))
    throw new ArgumentException("The A2A agent URL must use HTTP or HTTPS.");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var request = args.Length > 2 ? string.Join(' ', args.Skip(2)) : null;
var card = await new A2ACardResolver(agentUrl).GetAgentCardAsync(cancellation.Token);
Console.WriteLine($"=== Discovered A2A agent card ===\nName: {card.Name}\nDescription: {card.Description}");
foreach (var skill in card.Skills)
    Console.WriteLine($"Skill: {skill.Name} — {skill.Description}");

if (mode is "sync" or "all")
{
    Console.WriteLine("\n=== Synchronous A2A request (JSON-RPC) ===");
    var agent = card.AsAIAgent(options: new A2AClientOptions
    {
        PreferredBindings = [ProtocolBindingNames.JsonRpc]
    });
    var session = await agent.CreateSessionAsync(cancellation.Token);
    Console.WriteLine((await agent.RunAsync(
        request ?? "What is the exchange rate from USD to EUR?", session,
        cancellationToken: cancellation.Token)).Text);
}
if (mode is "stream" or "all")
{
    Console.WriteLine("\n=== Streaming A2A request (HTTP+JSON) ===");
    var agent = card.AsAIAgent(options: new A2AClientOptions
    {
        PreferredBindings = [ProtocolBindingNames.HttpJson]
    });
    var session = await agent.CreateSessionAsync(cancellation.Token);
    await foreach (var update in agent.RunStreamingAsync(
        request ?? "What's the exchange rate for JPY to GBP today?", session,
        cancellationToken: cancellation.Token))
        Console.Write(update.Text);
    Console.WriteLine();
}
