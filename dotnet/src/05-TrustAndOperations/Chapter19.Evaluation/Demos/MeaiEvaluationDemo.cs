using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;

namespace Chapter19.Evaluation.Demos;

public static class MeaiEvaluationDemo
{
    public static async Task RunAsync(IChatClient chatClient, CancellationToken cancellationToken = default)
    {
        var configuration = new ChatConfiguration(chatClient);
        Console.WriteLine("\n--- MEAI semantic equivalence: notebook paraphrase ---");
        IEvaluator equivalence = new EquivalenceEvaluator();
        var result = await equivalence.EvaluateAsync(
            [new ChatMessage(ChatRole.User, EvaluationSamples.CapitalQuery)],
            new ChatResponse(new ChatMessage(ChatRole.Assistant, EvaluationSamples.CapitalResponse)),
            configuration,
            additionalContext: [new EquivalenceEvaluatorContext(EvaluationSamples.CapitalReference)],
            cancellationToken: cancellationToken);
        PrintMetric(result.Get<NumericMetric>(EquivalenceEvaluator.EquivalenceMetricName));

        IEvaluator surveyEvaluator = new LegalSurveyEvaluator();
        foreach (var (label, question) in EvaluationSamples.SurveyQuestions)
        {
            Console.WriteLine($"\n--- MEAI legal survey quality: {label} ---");
            var evaluation = await surveyEvaluator.EvaluateAsync(
                [new ChatMessage(ChatRole.User, "Draft a legal survey question.")],
                new ChatResponse(new ChatMessage(ChatRole.Assistant, question)),
                configuration,
                cancellationToken: cancellationToken);
            PrintMetric(evaluation.Get<NumericMetric>(LegalSurveyEvaluator.MetricName));
        }
    }

    private static void PrintMetric(NumericMetric metric)
    {
        if (metric.Value is null || !double.IsFinite(metric.Value.Value) ||
            metric.Value is < 1 or > 5 || metric.ContainsDiagnostics())
        {
            throw new InvalidOperationException($"Evaluator '{metric.Name}' returned an unusable score: {metric.Reason}");
        }

        Console.WriteLine($"{metric.Name}: {metric.Value}/5\nReason: {metric.Reason}");
        Console.WriteLine($"Interpretation: {metric.Interpretation?.Rating}; failed={metric.Interpretation?.Failed}");
    }
}
