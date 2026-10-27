namespace Chapter17.Reasoning.Models;

public sealed record ReasoningReview(bool Accept, string Feedback);

public sealed record ResearchPlan(string[]? Queries);

public sealed record ResearchAssessment(
    bool SufficientCoverage,
    string[]? FollowUpQueries,
    string Summary);
