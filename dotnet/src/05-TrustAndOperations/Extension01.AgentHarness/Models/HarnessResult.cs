namespace Extension01.AgentHarness.Models;

public sealed record HarnessResult(
    string Status,
    string Answer,
    int ModelCalls,
    IReadOnlyList<TodoSnapshot> Todos,
    IReadOnlyList<SupportEvidence> Evidence,
    string? IncompleteReason);

public sealed record TodoSnapshot(int Id, string Title, bool IsComplete);

public sealed record SupportEvidence(string Service, string Kind, string Content);
