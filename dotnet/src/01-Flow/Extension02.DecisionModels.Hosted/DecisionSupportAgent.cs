using System.Text.Json;
using AgenticDesignPatterns.Shared.Hosting;
using ElBruno.AI.Decisions;
using Microsoft.Extensions.AI;

namespace Extension02.DecisionModels.Hosted;

public sealed class DecisionSupportAgent(IDecisionClient decisions, IChatClient chat) : StatelessWorkflowAgent
{
    public override string Name => "decision-models";
    public override string Description => "Typed support triage, priority, and review gates before a specialist draft.";

    protected override string GetInput(ChatMessage message)
    {
        if (message.Contents.Any(content => content is not TextContent))
        {
            throw new ArgumentException("This agent accepts text-only support requests.", nameof(message));
        }

        return base.GetInput(message);
    }

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await DecisionAgentWorkflow.RunAsync(
            decisions, chat, input, cancellationToken: cancellationToken));
}
