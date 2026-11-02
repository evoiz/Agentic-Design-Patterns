using AgenticDesignPatterns.Shared.Hosting;
using Chapter18.Guardrails;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter18.Guardrails.Hosted;

public sealed class GuardrailsHostedAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter18-guardrails";
    public override string Description =>
        "Screens caller requests with a structured policy evaluator before allowing the assistant to respond.";

    protected override async Task<string> RunAsync(string request, CancellationToken cancellationToken)
    {
        var response = await GuardrailsAgent.Create(chatClient)
            .RunAsync(request, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException("The guarded assistant returned an empty response.");
        }

        return response.Text;
    }
}
