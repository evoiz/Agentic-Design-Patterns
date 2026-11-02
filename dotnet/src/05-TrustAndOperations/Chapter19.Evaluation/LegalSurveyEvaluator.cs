using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Chapter19.Evaluation;

public sealed class LegalSurveyEvaluator : IEvaluator
{
    public const string MetricName = "Legal Survey Quality";
    public IReadOnlyCollection<string> EvaluationMetricNames => [MetricName];

    public async ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatConfiguration);
        var judgment = await LegalSurveyJudge.EvaluateAsync(
            chatConfiguration.ChatClient, modelResponse.Text, cancellationToken);
        var metric = new NumericMetric(MetricName, judgment.OverallScore, judgment.Rationale)
        {
            Interpretation = new EvaluationMetricInterpretation(
                judgment.OverallScore >= 4 ? EvaluationRating.Good : EvaluationRating.Poor,
                failed: judgment.OverallScore < 4,
                reason: "Demonstration threshold: >= 4/5. Calibrate against human reviews before gating releases.")
        };
        return new EvaluationResult(metric);
    }
}
