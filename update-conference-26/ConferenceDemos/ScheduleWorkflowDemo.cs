using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ConferenceDemos;

public static class ScheduleWorkflowDemo
{
    public static Task<string> RunAsync(
        IChatClient client, string question, CancellationToken cancellationToken = default,
        Action<WorkflowEvent>? onEvent = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        var prepare = new PrepareQuestion();
        var answer = new AnswerFromSchedule(client);
        var workflow = new WorkflowBuilder(prepare)
            .AddEdge(prepare, answer)
            .WithOutputFrom(answer)
            .Build();
        return ConferenceWorkflow.RunAsync<ScheduleQuestion, string>(
            workflow, new(question), cancellationToken, onEvent);
    }

    private sealed record ScheduleQuestion(string Question);

    private sealed class PrepareQuestion() : Executor<ScheduleQuestion, ScheduleQuestion>("PrepareQuestion")
    {
        public override ValueTask<ScheduleQuestion> HandleAsync(
            ScheduleQuestion message, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScheduleDemo.ValidateRequest(message.Question);
            return ValueTask.FromResult(message);
        }
    }

    private sealed class AnswerFromSchedule(IChatClient client) : Executor<ScheduleQuestion, string>("AnswerFromSchedule")
    {
        public override async ValueTask<string> HandleAsync(
            ScheduleQuestion message, IWorkflowContext context, CancellationToken cancellationToken = default)
            => await ScheduleDemo.RunAsync(client, message.Question, cancellationToken);
    }
}
