using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter01.PromptChaining.Demos;

internal static class PromptChainRunner
{
    public static async Task RunAsync(
        IChatClient chatClient,
        string input,
        string firstAgentName,
        string firstAgentInstructions,
        string secondAgentName,
        string secondAgentInstructions)
    {
        var bufferedChatClient = new BufferedStreamingChatClient(chatClient);
        var firstAgent = new ChatClientAgent(
            bufferedChatClient,
            firstAgentInstructions,
            firstAgentName);
        var secondAgent = new ChatClientAgent(
            bufferedChatClient,
            secondAgentInstructions,
            secondAgentName);

        var workflow = AgentWorkflowBuilder.BuildSequential([firstAgent, secondAgent]);
        var messages = new List<ChatMessage> { new(ChatRole.User, input) };

        await using var run = await InProcessExecution.RunAsync(workflow, messages);

        var agentOutputs = new Dictionary<string, StringBuilder>();
        List<ChatMessage>? finalMessages = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            if (workflowEvent is AgentResponseUpdateEvent responseUpdate)
            {
                if (!agentOutputs.TryGetValue(responseUpdate.ExecutorId, out var agentOutput))
                {
                    agentOutput = new StringBuilder();
                    agentOutputs.Add(responseUpdate.ExecutorId, agentOutput);
                }

                agentOutput.Append(responseUpdate.Update.Text);
            }
            else if (workflowEvent is WorkflowOutputEvent outputEvent)
            {
                finalMessages = outputEvent.As<List<ChatMessage>>();
            }
            else if (workflowEvent is ExecutorFailedEvent failedEvent)
            {
                var details = failedEvent.Data is Exception exception
                    ? DescribeException(exception)
                    : "No exception details were provided.";
                throw new InvalidOperationException(
                    $"Workflow executor '{failedEvent.ExecutorId}' failed: {details}");
            }
            else if (workflowEvent is WorkflowErrorEvent errorEvent)
            {
                throw new InvalidOperationException(
                    errorEvent.Exception is null
                        ? "The workflow reported an error without exception details."
                        : $"The workflow failed: {DescribeException(errorEvent.Exception)}");
            }
        }

        foreach (var (executorId, output) in agentOutputs)
        {
            Console.WriteLine($"\n--- {executorId} ---");
            Console.WriteLine(output);
        }

        var finalMessage = finalMessages?.LastOrDefault(message => message.Role == ChatRole.Assistant);
        if (finalMessage is null)
        {
            throw new InvalidOperationException(
                "The prompt chain completed without producing a final assistant response.");
        }

        Console.WriteLine("\n--- Final output ---");
        Console.WriteLine(finalMessage.Text);
    }

    private static string DescribeException(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join(" --> ", messages);
    }
}
