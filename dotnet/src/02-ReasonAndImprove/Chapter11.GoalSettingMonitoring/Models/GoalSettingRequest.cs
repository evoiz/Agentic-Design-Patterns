namespace Chapter11.GoalSettingMonitoring.Models;

public sealed record GoalSettingRequest(
    string UseCase,
    IReadOnlyList<string> Goals,
    int MaxIterations = 5)
{
    public const int MaximumIterations = 5;
    public const int MaximumUseCaseLength = 2000;
    public const int MaximumGoalLength = 300;

    public static IReadOnlyList<string> DefaultGoals { get; } =
        Array.AsReadOnly(
        [
            "Code simple to understand",
            "Functionally correct",
            "Handles comprehensive edge cases",
            "Takes positive integer input only",
            "prints the results with few examples"
        ]);

    public static GoalSettingRequest BinaryGapSample { get; } = new(
        "Write code to find BinaryGap of a given positive integer",
        DefaultGoals);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(UseCase);
        if (UseCase.Length > MaximumUseCaseLength)
        {
            throw new ArgumentException(
                $"The use case must be at most {MaximumUseCaseLength} characters.",
                nameof(UseCase));
        }

        ArgumentNullException.ThrowIfNull(Goals);
        if (Goals.Count is < 1 or > 10)
        {
            throw new ArgumentException("Provide between 1 and 10 goals.", nameof(Goals));
        }

        if (Goals.Any(goal =>
                string.IsNullOrWhiteSpace(goal) ||
                goal.Length > MaximumGoalLength))
        {
            throw new ArgumentException(
                $"Each goal must contain 1 to {MaximumGoalLength} non-whitespace characters.",
                nameof(Goals));
        }

        if (MaxIterations is < 1 or > MaximumIterations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxIterations),
                MaxIterations,
                $"The iteration budget must be between 1 and {MaximumIterations}.");
        }
    }
}
