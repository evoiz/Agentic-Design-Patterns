using Chapter15.A2A;

var mode = args.ElementAtOrDefault(0) ?? "all";
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

var request = args.Length > 2 ? string.Join(' ', args.Skip(2)) : null;
await A2AClientDemo.RunAsync(mode, agentUrl, request);
