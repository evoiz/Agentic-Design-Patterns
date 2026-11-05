using Microsoft.Agents.AI.Workflows;

namespace ConferenceDemos;

internal static class ConferenceWorkflow
{
    public static async Task<T> RunAsync<TInput, T>(Workflow workflow, TInput input, CancellationToken cancellationToken)
        where TInput : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var run = await InProcessExecution.RunAsync(workflow, input, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<T>();
        foreach (var evt in run.OutgoingEvents)
        {
            switch (evt)
            {
                case ExecutorFailedEvent failed:
                    throw new InvalidOperationException($"Executor '{failed.ExecutorId}' failed: {failed.Data}", failed.Data as Exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("Conference workflow failed.", error.Exception);
                case WorkflowOutputEvent output:
                    results.Add(output.As<T>() ?? throw new InvalidOperationException("The workflow returned an invalid result."));
                    break;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return results.Count == 1 ? results[0]
            : throw new InvalidOperationException($"Expected one final workflow result, received {results.Count}.");
    }
}
