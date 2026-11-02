using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Chapter03.Parallelization;

internal sealed class BufferedWorkflowChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // MAF consumes updates, but no branch tokens are exposed before fan-in.
        // Complete the model request inside the SDK's retry boundary first.
        var response = await InnerClient.GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }
}
