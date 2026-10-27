namespace Chapter04.Reflection;

public sealed record ReflectionWorkflowOptions(int MaxRounds = 3);

public enum ReflectionOutcome
{
    Accepted,
    MaxRoundsReached
}

public sealed record ReflectionState(
    string OriginalTask,
    int MaxRounds,
    string Draft = "",
    int RoundsUsed = 0,
    ReflectionReview? Review = null,
    ReflectionOutcome? Outcome = null);

public sealed record ReflectionResult(
    ReflectionOutcome Outcome,
    string Draft,
    ReflectionReview Review,
    int RoundsUsed,
    int MaxRounds);
