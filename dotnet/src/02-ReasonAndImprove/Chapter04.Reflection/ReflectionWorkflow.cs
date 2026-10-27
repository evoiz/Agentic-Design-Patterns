using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Chapter04.Reflection;

public static class ReflectionWorkflow
{
    public const string TelemetrySourceName = "Chapter04.Reflection";

    public static Workflow Create(
        AIAgent generator,
        AIAgent verifier,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(verifier);

        var generation = new GenerationExecutor(generator, writeOutput, cancellationToken);
        var verification = new VerificationExecutor(verifier, writeOutput, cancellationToken);
        var decision = new DecisionExecutor();
        var completion = new CompletionExecutor();

        return new WorkflowBuilder(generation)
            .AddEdge(generation, verification)
            .AddEdge(verification, decision)
            .AddEdge<ReflectionState>(decision, generation, state => state is { Outcome: null })
            .AddEdge<ReflectionState>(decision, completion, state => state is { Outcome: not null })
            .WithOutputFrom(completion)
            .Build();
    }

    public static async Task<ReflectionResult> RunAsync(
        AIAgent generator,
        AIAgent verifier,
        string task,
        ReflectionWorkflowOptions? options = null,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);
        options ??= new ReflectionWorkflowOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxRounds);
        cancellationToken.ThrowIfCancellationRequested();

        var workflow = Create(generator, verifier, writeOutput, cancellationToken);
        await using var run = await InProcessExecution.RunAsync(
            workflow,
            new ReflectionState(task, options.MaxRounds),
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        ReflectionResult? result = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case ExecutorFailedEvent failed:
                    throw new InvalidOperationException(
                        $"Reflection executor '{failed.ExecutorId}' failed: {failed.Data}",
                        failed.Data as Exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("The reflection workflow failed.", error.Exception);
                case WorkflowOutputEvent output:
                    if (result is not null)
                    {
                        throw new InvalidOperationException("The reflection workflow produced multiple terminal results.");
                    }

                    result = output.As<ReflectionResult>()
                        ?? throw new InvalidOperationException("The reflection workflow produced an invalid terminal result.");
                    break;
            }
        }

        return result ?? throw new InvalidOperationException("The reflection workflow completed without a terminal result.");
    }

    private sealed class GenerationExecutor(
        AIAgent generator, Action<string>? writeOutput, CancellationToken callerCancellation)
        : Executor<ReflectionState, ReflectionState>("Generation")
    {
        public override async ValueTask<ReflectionState> HandleAsync(
            ReflectionState state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, callerCancellation);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(state.OriginalTask);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(state.MaxRounds);
            if (state.Outcome is not null || state.RoundsUsed < 0 || state.RoundsUsed >= state.MaxRounds)
            {
                throw new InvalidOperationException("Generation cannot run outside the reflection round budget.");
            }

            var input = state.RoundsUsed == 0
                ? state.OriginalTask
                : $"""
                    Original task:
                    {state.OriginalTask}

                    Previous draft:
                    {state.Draft}

                    Independent review:
                    {state.Review?.Reasoning}

                    Revise the draft to address the review while preserving every original requirement.
                    Return only the complete revised draft.
                    """;
            var response = await generator.RunAsync(input, cancellationToken: linkedCancellation.Token);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException("The reflection generator returned an empty draft.");
            }

            var next = state with { Draft = response.Text, RoundsUsed = state.RoundsUsed + 1, Review = null };
            writeOutput?.Invoke($"\n--- Generated draft (round {next.RoundsUsed} of {next.MaxRounds}) ---\n{next.Draft}");
            return next;
        }
    }

    private sealed class VerificationExecutor(
        AIAgent verifier, Action<string>? writeOutput, CancellationToken callerCancellation)
        : Executor<ReflectionState, ReflectionState>("Verification")
    {
        public override async ValueTask<ReflectionState> HandleAsync(
            ReflectionState state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, callerCancellation);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            var response = await verifier.RunAsync<ReflectionReview>(
                $"Original task:\n{state.OriginalTask}\n\nDraft to verify:\n{state.Draft}",
                cancellationToken: linkedCancellation.Token);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            var review = response.Result;
            if (review?.Decision is not { } verdict ||
                !Enum.IsDefined(verdict) ||
                string.IsNullOrWhiteSpace(review.Reasoning))
            {
                throw new InvalidOperationException("The reflection verifier returned an incomplete or unsupported structured review.");
            }

            writeOutput?.Invoke($"\n--- Independent verification ---\nDecision: {verdict}\n{review.Reasoning}");
            return state with { Review = review };
        }
    }

    private sealed class DecisionExecutor() : Executor<ReflectionState, ReflectionState>("Decision")
    {
        public override ValueTask<ReflectionState> HandleAsync(
            ReflectionState state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var outcome = state.Review?.Decision switch
            {
                ReflectionDecision.Accept => ReflectionOutcome.Accepted,
                ReflectionDecision.Revise when state.RoundsUsed >= state.MaxRounds => ReflectionOutcome.MaxRoundsReached,
                ReflectionDecision.Revise => (ReflectionOutcome?)null,
                _ => throw new InvalidOperationException("The reflection decision requires a validated review.")
            };
            return ValueTask.FromResult(state with { Outcome = outcome });
        }
    }

    [YieldsOutput(typeof(ReflectionResult))]
    private sealed class CompletionExecutor() : Executor<ReflectionState>("Completion")
    {
        public override async ValueTask HandleAsync(
            ReflectionState state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            if (state.Outcome is not { } outcome || state.Review is null)
            {
                throw new InvalidOperationException("Reflection cannot complete without an outcome and review.");
            }

            await context.YieldOutputAsync(
                new ReflectionResult(outcome, state.Draft, state.Review, state.RoundsUsed, state.MaxRounds),
                cancellationToken);
        }
    }
}
