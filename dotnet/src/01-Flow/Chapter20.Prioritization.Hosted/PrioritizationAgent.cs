using AgenticDesignPatterns.Shared.Hosting;
using System.Text.Json;
using Chapter20.Prioritization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter20.Prioritization.Hosted;

public sealed class PrioritizationAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter20-prioritization";
    public override string Description =>
        "Proposes priorities for caller-supplied tasks and returns a deterministic priority queue.";

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken)
    {
        var taskDescriptions = ParseTaskDescriptions(input);
        var schedule = await PrioritizationWorkflow.RunAsync(
            chatClient,
            taskDescriptions,
            cancellationToken: cancellationToken);
        var result = string.Join(
            Environment.NewLine,
            schedule.Select((task, index) =>
                $"{index + 1}. {task.Id} | {task.Priority} | {task.AssignedTo} | {task.Description}"));
        return result;
    }

    private static IReadOnlyList<string> ParseTaskDescriptions(string input)
    {
        var trimmedInput = input.Trim();
        if (!trimmedInput.StartsWith('{'))
        {
            return trimmedInput.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        TaskBatchRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<TaskBatchRequest>(trimmedInput, JsonSerializerOptions.Web);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "JSON task requests must have the shape {\"tasks\":[\"description\", ...]}.",
                nameof(input),
                exception);
        }

        return request?.Tasks
            ?? throw new ArgumentException(
                "JSON task requests must include a non-null 'tasks' array.",
                nameof(input));
    }

    private sealed record TaskBatchRequest(IReadOnlyList<string>? Tasks);
}
