namespace Chapter09.LearningAdaptation.Models;

public sealed record EvaluationCaseResult(
    string Name,
    bool Passed,
    IReadOnlyList<string> MissingPhrases,
    IReadOnlyList<string> ForbiddenPhrases);

public sealed record PromptEvaluationResult(
    string Instructions,
    int PassedCases,
    int TotalCases,
    IReadOnlyList<EvaluationCaseResult> Cases)
{
    public bool Passed => PassedCases == TotalCases;
}

public sealed record PromptProposal(
    int Iteration,
    PromptEvaluationResult Evaluation);

public sealed record LearningAdaptationResult(
    PromptEvaluationResult Baseline,
    IReadOnlyList<PromptProposal> Proposals,
    PromptEvaluationResult Best)
{
    public bool Improved => Best.PassedCases > Baseline.PassedCases;

    private string Recommendation =>
        Proposals.Count == 0
            ? "The starting prompt already passes the benchmark; no update is recommended."
            : Improved
                ? "An improved prompt is available for human review."
                : "No candidate improved the benchmark; keep the starting prompt.";

    public string Format() =>
        $"""
        Baseline score: {Baseline.PassedCases}/{Baseline.TotalCases} cases passed.
        Best score: {Best.PassedCases}/{Best.TotalCases} cases passed.
        Adaptation attempts: {Proposals.Count}
        Prompt improved: {(Improved ? "yes" : "no")}
        No prompt is promoted or persisted automatically; review the proposal before adopting it.
        Recommendation: {Recommendation}

        Best-scoring prompt:
        {Best.Instructions}
        """;
}
