namespace Chapter11.GoalSettingMonitoring.Models;

public sealed record GoalSettingProgress(
    GoalSettingRequest Request,
    IReadOnlyList<GoalSettingIteration> Iterations,
    string? CurrentCode = null,
    string? CurrentFeedback = null,
    bool IsComplete = false);
