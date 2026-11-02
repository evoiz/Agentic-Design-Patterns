using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Chapter19.Evaluation.Demos;

public static class LegalSurveyDemo
{
    public static async Task RunAsync(IChatClient chatClient, CancellationToken cancellationToken = default)
    {
        foreach (var (label, question) in EvaluationSamples.SurveyQuestions)
        {
            Console.WriteLine($"\n--- Legal survey judge: {label} ---\n{question}");
            var judgment = await LegalSurveyJudge.EvaluateAsync(chatClient, question, cancellationToken);
            Console.WriteLine(JsonSerializer.Serialize(judgment, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
