using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter08.Memory.Demos;

// Session-scoped memory: explicit facts are written by code and injected into every later model call.
internal sealed class ConversationMemoryProvider : AIContextProvider
{
    private readonly ProviderSessionState<ConversationMemoryState> _sessionState = new(
        stateInitializer: _ => new ConversationMemoryState(),
        stateKey: nameof(ConversationMemoryProvider));

    public override IReadOnlyList<string> StateKeys => [_sessionState.StateKey];

    public ConversationMemoryState GetState(AgentSession session) =>
        _sessionState.GetOrInitializeState(session);

    // Explicit state update: deterministic code, not the model, decides what is remembered.
    public void Remember(AgentSession session, string key, string value)
    {
        var state = _sessionState.GetOrInitializeState(session);
        state.Facts[key] = value;
        _sessionState.SaveState(session, state);
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

    // Output-key style capture: store the latest assistant answer in session state.
    protected override ValueTask StoreAIContextAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = _sessionState.GetOrInitializeState(context.Session);
        state.LastResponse = context.ResponseMessages?
            .LastOrDefault(message => message.Role == ChatRole.Assistant)?
            .Text;
        _sessionState.SaveState(context.Session, state);
        return ValueTask.CompletedTask;
    }
}

internal sealed class ConversationMemoryState
{
    public Dictionary<string, string> Facts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastResponse { get; set; }
}
