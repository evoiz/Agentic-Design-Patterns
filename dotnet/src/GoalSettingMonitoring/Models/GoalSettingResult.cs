namespace GoalSettingMonitoring.Models;

public sealed record GoalSettingIteration(
    int Number,
    string Code,
    string Feedback,
    bool GoalsMet,
    string Decision,
    bool HardCriteriaMet = true,
    string? HardCriteriaFeedback = null);

public sealed record GoalSettingResult(
    GoalSettingRequest Request,
    IReadOnlyList<GoalSettingIteration> Iterations)
{
    public string FinalCode => Iterations[^1].Code;
    public string FinalArtifact => FinalCode;
    public bool GoalsMet => Iterations[^1].GoalsMet;
    public bool HardCriteriaMet => Iterations[^1].HardCriteriaMet;

    public string Format()
    {
        var hardCriteriaSummary = Request.Scenario == GoalSettingScenario.DotNetDeliverable
            ? $"\nHard criteria: {(HardCriteriaMet ? "met" : "not met")}" +
              (string.IsNullOrWhiteSpace(Iterations[^1].HardCriteriaFeedback)
                  ? string.Empty
                  : $"\nHard-criteria feedback: {Iterations[^1].HardCriteriaFeedback}")
            : string.Empty;
        var artifactName = Request.Scenario == GoalSettingScenario.PythonCode
            ? "Python code"
            : ".NET implementation brief";

        return $"""
            Goal status: {(GoalsMet ? "met" : $"not met within {Request.MaxIterations} iterations")}
            Iterations used: {Iterations.Count}{hardCriteriaSummary}

            --- Final {artifactName} ---
            {FinalCode}
            """;
    }
}
