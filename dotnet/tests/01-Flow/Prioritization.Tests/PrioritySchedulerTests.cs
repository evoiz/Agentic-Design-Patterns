using Chapter20.Prioritization;
using Chapter20.Prioritization.Models;
using Xunit;

namespace Prioritization.Tests;

public sealed class PrioritySchedulerTests
{
    [Fact]
    public void Schedule_OrdersByPriorityAndPreservesArrivalOrderForTies()
    {
        var tasks = new[]
        {
            new ProjectTask("TASK-001", "First medium", TaskPriority.P1, "Worker A"),
            new ProjectTask("TASK-002", "Low", TaskPriority.P2, "Worker B"),
            new ProjectTask("TASK-003", "First urgent", TaskPriority.P0, "Worker B"),
            new ProjectTask("TASK-004", "Second urgent", TaskPriority.P0, "Review Team"),
            new ProjectTask("TASK-005", "Second medium", TaskPriority.P1, "Worker A")
        };

        var scheduled = PriorityScheduler.Schedule(tasks);

        Assert.Equal(
            ["TASK-003", "TASK-004", "TASK-001", "TASK-005", "TASK-002"],
            scheduled.Select(task => task.Id));
        Assert.Equal(
            ["TASK-001", "TASK-002", "TASK-003", "TASK-004", "TASK-005"],
            tasks.Select(task => task.Id));
    }

    [Fact]
    public void Schedule_RejectsDuplicateTaskIds()
    {
        var tasks = new[]
        {
            new ProjectTask("TASK-001", "First", TaskPriority.P0, "Worker A"),
            new ProjectTask("TASK-001", "Second", TaskPriority.P1, "Worker B")
        };

        var exception = Assert.Throws<ArgumentException>(() => PriorityScheduler.Schedule(tasks));

        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Schedule_RejectsInvalidTaskState()
    {
        var tasks = new[]
        {
            new ProjectTask("TASK-001", "Task", (TaskPriority)99, "Worker A")
        };

        Assert.Throws<ArgumentException>(() => PriorityScheduler.Schedule(tasks));
    }
}
