namespace Chapter20.Prioritization.Models;

public enum TaskPriority
{
    P0,
    P1,
    P2
}

public sealed record ProjectTask(
    string Id,
    string Description,
    TaskPriority Priority,
    string AssignedTo);

public sealed record PriorityProposal(string Priority, string? AssignedTo);
