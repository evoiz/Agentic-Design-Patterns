using System.Text.Json.Serialization;

namespace Chapter19.Evaluation.Models;

public sealed record SurveyJudgment(
    [property: JsonPropertyName("overall_score")] int OverallScore,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("detailed_feedback")] string[] DetailedFeedback,
    [property: JsonPropertyName("concerns")] string[] Concerns,
    [property: JsonPropertyName("recommended_action")] string RecommendedAction)
{
    public void Validate()
    {
        if (OverallScore is < 1 or > 5 ||
            string.IsNullOrWhiteSpace(Rationale) ||
            string.IsNullOrWhiteSpace(RecommendedAction) ||
            DetailedFeedback is null || DetailedFeedback.Length < 5 ||
            DetailedFeedback.Any(string.IsNullOrWhiteSpace) ||
            new[] { "clarity", "neutrality", "relevance", "completeness", "audience" }
                .Any(criterion => !DetailedFeedback.Any(feedback => feedback.Contains(criterion, StringComparison.OrdinalIgnoreCase))) ||
            Concerns is null || Concerns.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "The legal survey judge must return a score from 1 to 5, a rationale, " +
                "feedback for all five criteria, a concerns array, and a recommended action.");
        }
    }
}
