using Microsoft.Extensions.AI;

namespace Chapter18.Guardrails.Demos;

public static class ToolParameterValidationDemo
{
    private const string AuthenticatedUserId = "user-1001";
    private static readonly string[] Requests =
    [
        "Read the profile for user-1001.",
        "Read the profile for user-2002."
    ];

    public static async Task RunAsync(
        IChatClient chatClient,
        CancellationToken cancellationToken = default)
    {
        var functions = new GuardrailFunctions(
            AuthenticatedUserId,
            message => Console.WriteLine($"[tool guard] {message}"));
        var agent = GuardrailsAgent.CreateToolValidationAgent(chatClient, functions);

        foreach (var request in Requests)
        {
            Console.WriteLine($"\n--- Tool validation request ---\n{request}");
            var response = await agent.RunAsync(request, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException("The tool-validation agent returned an empty response.");
            }

            Console.WriteLine($"\n--- Tool-validation response ---\n{response.Text}");
        }
    }
}
