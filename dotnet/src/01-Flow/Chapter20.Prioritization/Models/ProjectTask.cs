using System.Text.Json.Serialization;

namespace Chapter20.Prioritization.Models;

[JsonConverter(typeof(TaskPriorityConverter))]
public enum TaskPriority
{
    P0,
    P1,
    P2
}

public sealed class TaskPriorityConverter() : JsonStringEnumConverter<TaskPriority>(allowIntegerValues: false);

public sealed record ProjectTask(
    string Id,
    string Description,
    TaskPriority Priority,
    string AssignedTo);

public sealed record PriorityProposal(TaskPriority? Priority, string? AssignedTo);
