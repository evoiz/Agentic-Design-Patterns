using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent;

public static partial class MultiAgentWorkflow
{
    private static async Task<string> RunCoordinatorAsync(
        IChatClient chatClient, string input, Action<string>? writeOutput, CancellationToken cancellationToken)
    {
        var greeter = CreateAgent(chatClient, "Greeter", "Greet the user warmly and briefly. Return only the greeting.");
        var taskExecutor = AIFunctionFactory.Create(
            (string task) => $"Task completed by the deterministic demonstration executor: {task.Trim()}",
            name: "execute_task",
            description: "Performs a deterministic demonstration task and reports its completion.");
        var coordinator = CreateAgent(chatClient, "Coordinator", """
            Choose exactly one appropriate delegate:
            - use Greeter for a request to greet or say hello
            - use execute_task for a request to perform a task
            Return the delegate's result without inventing extra actions.
            """, greeter.AsAIFunction(), taskExecutor);
        var response = await coordinator.RunAsync(input, cancellationToken: cancellationToken);
        return WriteResult("Coordinator delegation result", response.Text, writeOutput);
    }
}
