using Microsoft.Extensions.AI;

namespace Extension01.AgentHarness;

internal sealed class ModelCallBudgetClient(IChatClient innerClient, int maximumCalls) : DelegatingChatClient(innerClient)
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref _calls) > maximumCalls)
        {
            throw new InvalidOperationException($"The harness exhausted its {maximumCalls}-model-call budget.");
        }

        return base.GetResponseAsync(messages, options, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The harness uses non-streaming model calls so its call budget is enforced.");
}
