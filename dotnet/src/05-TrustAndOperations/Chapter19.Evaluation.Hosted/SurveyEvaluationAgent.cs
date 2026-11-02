using AgenticDesignPatterns.Shared.Hosting;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter19.Evaluation.Hosted;

public sealed class SurveyEvaluationAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter19-evaluation";
    public override string Description => "Reviews the caller's legal survey question against five methodology criteria.";

    protected override async Task<string> RunAsync(string question, CancellationToken cancellationToken)
    {
        var judgment = await LegalSurveyJudge.EvaluateAsync(chatClient, question, cancellationToken);
        return JsonSerializer.Serialize(judgment);
    }
}
