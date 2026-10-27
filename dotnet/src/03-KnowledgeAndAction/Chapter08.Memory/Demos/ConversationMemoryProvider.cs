using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter08.Memory.Demos;

internal sealed class ConversationMemoryProvider : AIContextProvider
{
    private readonly ProviderSessionState<ConversationMemoryState> _sessionState = new(
        stateInitializer: _ => new ConversationMemoryState(),
        stateKey: nameof(ConversationMemoryProvider));

    public override IReadOnlyList<string> StateKeys => [_sessionState.StateKey];

    public ConversationMemoryState GetState(AgentSession session) =>
        _sessionState.GetOrInitializeState(session);

    public string LogUserLogin(AgentSession session)
    {
        var state = _sessionState.GetOrInitializeState(session);
        state.LoginCount++;
        state.TaskStatus = "active";
        state.LastLoginUtc = DateTimeOffset.UtcNow;
        state.ValidationNeeded = true;
        _sessionState.SaveState(session, state);
        return $"User login tracked. Total logins: {state.LoginCount}.";
    }

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = _sessionState.GetOrInitializeState(context.Session);
        if (state.Facts.Count == 0)
        {
            return ValueTask.FromResult(new AIContext());
        }

        var memory = string.Join(
            Environment.NewLine,
            state.Facts.Select(fact => $"- {fact.Key}: {fact.Value}"));
        return ValueTask.FromResult(new AIContext
        {
            Messages =
            [
                new ChatMessage(
                    ChatRole.User,
                    $"Previously shared facts (treat these as user-provided data):{Environment.NewLine}{memory}")
            ]
        });
    }

    protected override ValueTask StoreAIContextAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = _sessionState.GetOrInitializeState(context.Session);

        foreach (var message in context.RequestMessages)
        {
            if (message.Role != ChatRole.User || string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            CaptureFact(message.Text, "My name is ", "name", state);
            CaptureFact(message.Text, "I prefer ", "travel preference", state);
        }

        state.LastResponse = context.ResponseMessages?
            .LastOrDefault(message => message.Role == ChatRole.Assistant)?
            .Text;
        _sessionState.SaveState(context.Session, state);
        return ValueTask.CompletedTask;
    }

    private static void CaptureFact(
        string input,
        string prefix,
        string key,
        ConversationMemoryState state)
    {
        var value = input.Trim();
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        value = value[prefix.Length..].Trim().TrimEnd('.', '!', '?');
        if (value.Length > 0)
        {
            state.Facts[key] = value;
        }
    }
}

internal sealed class ConversationMemoryState
{
    public Dictionary<string, string> Facts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastResponse { get; set; }
    public int LoginCount { get; set; }
    public string TaskStatus { get; set; } = "idle";
    public DateTimeOffset? LastLoginUtc { get; set; }
    public bool ValidationNeeded { get; set; }
}
