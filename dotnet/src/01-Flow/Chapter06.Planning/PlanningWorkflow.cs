using Chapter06.Planning.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Chapter06.Planning;

public static class PlanningWorkflow
{
    public const string TelemetrySourceName = "Chapter06.Planning";

    public static async Task<PlanningResult> RunAsync(
        AIAgent planner,
        AIAgent writer,
        string topic,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        cancellationToken.ThrowIfCancellationRequested();

        var planExecutor = new PlannerExecutor(planner, writeOutput);
        var writerExecutor = new WriterExecutor(writer, writeOutput);
        var completionExecutor = new CompletionExecutor();
        var workflow = new WorkflowBuilder(planExecutor)
            .AddEdge(planExecutor, writerExecutor)
            .AddEdge(writerExecutor, completionExecutor)
            .WithOutputFrom(completionExecutor)
            .Build();

        await using var run = await InProcessExecution.RunAsync(
            workflow,
            new PlanningRequest(topic),
            cancellationToken: cancellationToken);

        PlanningResult? result = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case ExecutorFailedEvent failed:
                    throw new InvalidOperationException(
                        $"Planning executor '{failed.ExecutorId}' failed: {failed.Data}",
                        failed.Data as Exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("The planning workflow failed.", error.Exception);
                case WorkflowOutputEvent output:
                    if (result is not null)
                    {
                        throw new InvalidOperationException("The planning workflow produced multiple final results.");
                    }

                    result = output.As<PlanningResult>()
                        ?? throw new InvalidOperationException("The planning workflow produced an invalid final result.");
                    break;
            }
        }

        return result
            ?? throw new InvalidOperationException("The planning workflow completed without a final result.");
    }

    private sealed record PlanningRequest(string Topic);

    private sealed record PlannedTopic(string Topic, IReadOnlyList<string> Steps);

    private sealed record WrittenSummary(IReadOnlyList<string> Steps, string Summary);

    private sealed class PlannerExecutor(AIAgent planner, Action<string>? writeOutput)
        : Executor<PlanningRequest, PlannedTopic>("Planner")
    {
        public override async ValueTask<PlannedTopic> HandleAsync(
            PlanningRequest request,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Topic);
            var response = await planner.RunAsync<PlanningPlan>(
                request.Topic,
                cancellationToken: cancellationToken);
            var steps = response.Result?.Steps;
            if (steps is null || steps.Count is < 3 or > 6 || steps.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    "The planner must return between three and six nonempty steps.");
            }

            var normalizedSteps = steps.Select(step => step.Trim()).ToArray();
            writeOutput?.Invoke("\n--- Plan ---");
            foreach (var (step, index) in normalizedSteps.Select((step, index) => (step, index + 1)))
            {
                writeOutput?.Invoke($"{index}. {step}");
            }

            return new PlannedTopic(request.Topic, normalizedSteps);
        }
    }

    private sealed class WriterExecutor(AIAgent writer, Action<string>? writeOutput)
        : Executor<PlannedTopic, WrittenSummary>("Writer")
    {
        public override async ValueTask<WrittenSummary> HandleAsync(
            PlannedTopic plan,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var numberedPlan = string.Join(
                Environment.NewLine,
                plan.Steps.Select((step, index) => $"{index + 1}. {step}"));
            var response = await writer.RunAsync(
                $"""
                Topic: {plan.Topic}

                Approved plan:
                {numberedPlan}

                Write an informative summary of about 200 words on the topic, following the plan.
                Do not include a second plan section. Return only the summary text.
                """,
                cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException("The writer returned an empty summary.");
            }

            writeOutput?.Invoke("\n--- Summary ---");
            writeOutput?.Invoke(response.Text);
            return new WrittenSummary(plan.Steps, response.Text);
        }
    }

    [YieldsOutput(typeof(PlanningResult))]
    private sealed class CompletionExecutor() : Executor<WrittenSummary>("Completion")
    {
        public override async ValueTask HandleAsync(
            WrittenSummary result,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            await context.YieldOutputAsync(
                new PlanningResult(result.Steps, result.Summary),
                cancellationToken);
        }
    }
}
