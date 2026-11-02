using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Chapter01.PromptChaining.Demos;

internal sealed class BufferedStreamingChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await InnerClient.GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
        {
            yield return new ChatResponseUpdate(message.Role, message.Text);
        }
    }
}
