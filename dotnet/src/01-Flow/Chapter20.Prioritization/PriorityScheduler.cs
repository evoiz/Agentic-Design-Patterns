using Chapter20.Prioritization.Models;

namespace Chapter20.Prioritization;

public static class PriorityScheduler
{
    public static IReadOnlyList<ProjectTask> Schedule(IReadOnlyList<ProjectTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Any(task => task is null))
        {
            throw new ArgumentException("The schedule cannot contain null tasks.", nameof(tasks));
        }

        if (tasks.Any(task =>
                string.IsNullOrWhiteSpace(task.Id) ||
                string.IsNullOrWhiteSpace(task.Description) ||
                string.IsNullOrWhiteSpace(task.AssignedTo) ||
                !Enum.IsDefined(task.Priority)))
        {
            throw new ArgumentException(
                "Every task must have an ID, description, assigned worker, and valid priority.",
                nameof(tasks));
        }

        if (tasks.Select(task => task.Id).Distinct(StringComparer.Ordinal).Count() != tasks.Count)
        {
            throw new ArgumentException("Task IDs must be unique within a schedule.", nameof(tasks));
        }

        return tasks
            .Select((task, arrivalOrder) => (Task: task, ArrivalOrder: arrivalOrder))
            .OrderBy(item => item.Task.Priority)
            .ThenBy(item => item.ArrivalOrder)
            .Select(item => item.Task)
            .ToArray();
    }
}
