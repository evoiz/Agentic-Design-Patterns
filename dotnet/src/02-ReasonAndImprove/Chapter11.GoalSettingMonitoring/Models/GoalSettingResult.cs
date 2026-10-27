namespace Chapter11.GoalSettingMonitoring.Models;

public sealed record GoalSettingIteration(
    int Number,
    string Code,
    string Feedback,
    bool GoalsMet,
    string Decision);

public sealed record GoalSettingResult(
    GoalSettingRequest Request,
    IReadOnlyList<GoalSettingIteration> Iterations)
{
    public string FinalCode => Iterations[^1].Code;
    public bool GoalsMet => Iterations[^1].GoalsMet;

    public string Format() =>
        $"""
        Goal status: {(GoalsMet ? "met" : $"not met within {Request.MaxIterations} iterations")}
        Iterations used: {Iterations.Count}

        --- Final Python code ---
        {FinalCode}
        """;
}
