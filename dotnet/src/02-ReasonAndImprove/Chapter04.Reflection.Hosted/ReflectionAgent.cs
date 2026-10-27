using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter04.Reflection;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter04.Reflection.Hosted;

public sealed class ReflectionAgent(IChatClient chatClient) : AIAgent
{
    public override string Name => "chapter04-reflection";
    public override string Description =>
        "Generates content, obtains an independent structured review, and refines within a fixed limit.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var (scenario, input) = ParseRequest(request);
        var output = await ReflectionDemos.RunAsync(
            chatClient,
            scenario,
            input,
            cancellationToken: cancellationToken);
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, output));
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await RunCoreAsync(messages, session, options, cancellationToken);
        yield return new AgentResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent(response.Text)]
        };
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        => new(new ReflectionSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
        => new(new ReflectionSession());

    private static (ReflectionScenario Scenario, string Input) ParseRequest(string request)
    {
        const string factCheckPrefix = "fact-check:";
        const string productPrefix = "product:";
        const string codeReviewPrefix = "code-review:";

        if (request.StartsWith(factCheckPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (ReflectionScenario.FactCheck, GetInput(request, factCheckPrefix));
        }

        if (request.StartsWith(productPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (ReflectionScenario.ProductDescription, GetInput(request, productPrefix));
        }

        if (request.StartsWith(codeReviewPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (ReflectionScenario.IterativeCodeReview, GetInput(request, codeReviewPrefix));
        }

        return (ReflectionScenario.ProductDescription, request);
    }

    private static string GetInput(string request, string prefix)
    {
        var input = request[prefix.Length..].Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return input;
    }

    private sealed class ReflectionSession : AgentSession;
}
