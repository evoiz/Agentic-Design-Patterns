using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter01.PromptChaining.Demos;

public static class PromptChainRunner
{
    private static readonly JsonSerializerOptions OutputJson = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public static async Task<string> RunAsync<TOutput>(
        IChatClient chatClient,
        string input,
        string firstAgentName,
        string firstAgentInstructions,
        string secondAgentName,
        string secondAgentInstructions,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        writeOutput ??= Console.WriteLine;
        // BuildSequential uses the streaming agent entry point, even for buffered workflow runs.
        var client = new BufferedStreamingChatClient(chatClient);
        var firstAgent = new ChatClientAgent(client, firstAgentInstructions, firstAgentName);
        var secondAgent = new ChatClientAgent(client, new ChatClientAgentOptions
        {
            Name = secondAgentName,
            ChatOptions = new()
            {
                Instructions = secondAgentInstructions,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<TOutput>(OutputJson)
            }
        });
        var workflow = AgentWorkflowBuilder.BuildSequential([firstAgent, secondAgent]);
        await using var run = await InProcessExecution.RunAsync(
            workflow, new List<ChatMessage> { new(ChatRole.User, input) },
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        List<ChatMessage>? messages = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case ExecutorFailedEvent failed:
                    throw new InvalidOperationException($"Prompt chain executor '{failed.ExecutorId}' failed: {failed.Data}", failed.Data as Exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("The prompt chain failed.", error.Exception);
                case WorkflowOutputEvent output:
                    messages = output.As<List<ChatMessage>>();
                    break;
            }
        }

        var responses = messages?.Where(message => message.Role == ChatRole.Assistant).ToArray() ?? [];
        foreach (var response in responses)
        {
            writeOutput($"\n--- {response.AuthorName ?? "Agent output"} ---\n{response.Text}");
        }

        var final = responses.LastOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(final) ||
            JsonSerializer.Deserialize<TOutput>(final, OutputJson) is null)
        {
            throw new InvalidOperationException("The prompt chain did not return a structured final response.");
        }

        writeOutput($"\n--- Final output ---\n{final}");
        return final;
    }
}
