using Chapter11.GoalSettingMonitoring.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter11.GoalSettingMonitoring;

public static class GoalSettingWorkflow
{
    public const string TelemetrySourceName = "Chapter11.GoalSettingMonitoring";

    // generate -> review -> judge, looping back to generate until the judge says True or the budget is spent.
    public static async Task<GoalSettingResult> RunAsync(
        IChatClient chatClient,
        GoalSettingRequest request,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var generation = new GenerationExecutor(CreateAgent(chatClient, "CodeGenerator", """
            You are a Python code generator. Return only a complete Python code snippet that addresses
            the caller's use case and goals. Do not include Markdown fences or explanations.
            When revising, preserve the use case and address the reviewer feedback without dropping
            previously satisfied goals.
            """), writeOutput);
        var review = new ReviewExecutor(CreateAgent(chatClient, "GoalReviewer", """
            You are an independent Python code reviewer. Critique the submitted code against every
            stated goal. Identify correctness, clarity, and edge-case gaps. Do not rewrite the code
            and do not decide whether the loop should stop; the separate goal judge makes that decision.
            """), writeOutput);
        var assessment = new AssessmentExecutor(CreateAgent(chatClient, "GoalJudge", """
            You are a strict goal-completion judge. Decide whether the code satisfies every stated
            goal, using the review as evidence. Reply with exactly one word: True or False.
            """), writeOutput);
        var completion = new CompletionExecutor();
        var workflow = new WorkflowBuilder(generation)
            .AddEdge(generation, review)
            .AddEdge(review, assessment)
            .AddEdge<GoalSettingProgress>(assessment, generation, state => state is { IsComplete: false })
            .AddEdge<GoalSettingProgress>(assessment, completion, state => state is { IsComplete: true })
            .WithOutputFrom(completion)
            .Build();

        await using var run = await InProcessExecution.RunAsync(
            workflow,
            new GoalSettingProgress(request, []),
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        GoalSettingResult? result = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case ExecutorFailedEvent failed:
                    throw new InvalidOperationException(
                        $"Goal-setting executor '{failed.ExecutorId}' failed: {failed.Data}",
                        failed.Data as Exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("The goal-setting workflow failed.", error.Exception);
                case WorkflowOutputEvent output when result is not null:
                    throw new InvalidOperationException("The goal-setting workflow produced multiple terminal results.");
                case WorkflowOutputEvent output:
                    result = output.As<GoalSettingResult>()
                        ?? throw new InvalidOperationException("The goal-setting workflow produced an invalid terminal result.");
                    break;
            }
        }

        return result ?? throw new InvalidOperationException(
            "The goal-setting workflow completed without a terminal result.");
    }

    private static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions) =>
        new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();

    private static string FormatGoals(IEnumerable<string> goals) =>
        string.Join(Environment.NewLine, goals.Select(goal => $"- {goal.Trim()}"));

    // The generator is told not to use fences; strip them anyway so the saved file is valid Python.
    private static string CleanCode(string? text)
    {
        var lines = text?.Trim().Split('\n').ToList() ?? [];
        if (lines.Count > 0 && lines[0].TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count > 0 && lines[^1].Trim() == "```")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines).Trim();
    }

    private sealed class GenerationExecutor(AIAgent generator, Action<string>? writeOutput)
        : Executor<GoalSettingProgress, GoalSettingProgress>("CodeGeneration")
    {
        public override async ValueTask<GoalSettingProgress> HandleAsync(
            GoalSettingProgress state, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            if (state.IsComplete || state.Iterations.Count >= state.Request.MaxIterations)
            {
                throw new InvalidOperationException(
                    "Code generation cannot run after goal completion or outside the iteration budget.");
            }

            var number = state.Iterations.Count + 1;
            writeOutput?.Invoke($"\n--- Code generation (iteration {number} of {state.Request.MaxIterations}) ---");
            var input = $"Use case:\n{state.Request.UseCase}\n\nGoals:\n{FormatGoals(state.Request.Goals)}";
            if (state.Iterations.Count > 0)
            {
                var previous = state.Iterations[^1];
                input += $"\n\nPreviously generated code:\n{previous.Code}\n\nReviewer feedback:\n{previous.Feedback}";
            }

            var response = await generator.RunAsync(input, cancellationToken: cancellationToken);
            var code = CleanCode(response.Text);
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("The code generator returned an empty Python program.");
            }

            writeOutput?.Invoke($"\n--- Generated code (iteration {number}) ---\n{code}");
            return state with { CurrentCode = code, CurrentFeedback = null };
        }
    }

    private sealed class ReviewExecutor(AIAgent reviewer, Action<string>? writeOutput)
        : Executor<GoalSettingProgress, GoalSettingProgress>("GoalReview")
    {
        public override async ValueTask<GoalSettingProgress> HandleAsync(
            GoalSettingProgress state, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            var code = state.CurrentCode
                ?? throw new InvalidOperationException("Goal review requires generated code.");
            writeOutput?.Invoke($"\n--- Goal review (iteration {state.Iterations.Count + 1}) ---");
            var response = await reviewer.RunAsync(
                $"Use case:\n{state.Request.UseCase}\n\nGoals:\n{FormatGoals(state.Request.Goals)}\n\nCode:\n{code}",
                cancellationToken: cancellationToken);
            var feedback = response.Text?.Trim();
            if (string.IsNullOrWhiteSpace(feedback))
            {
                throw new InvalidOperationException("The goal reviewer returned empty feedback.");
            }

            writeOutput?.Invoke(feedback);
            return state with { CurrentFeedback = feedback };
        }
    }

    // Records the judge's verdict and decides deterministically whether the bounded loop stops.
    private sealed class AssessmentExecutor(AIAgent judge, Action<string>? writeOutput)
        : Executor<GoalSettingProgress, GoalSettingProgress>("GoalAssessment")
    {
        public override async ValueTask<GoalSettingProgress> HandleAsync(
            GoalSettingProgress state, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(state.CurrentCode) || string.IsNullOrWhiteSpace(state.CurrentFeedback))
            {
                throw new InvalidOperationException(
                    "Goal assessment requires both generated code and reviewer feedback.");
            }

            var number = state.Iterations.Count + 1;
            writeOutput?.Invoke($"\n--- Goal decision (iteration {number}) ---");
            var response = await judge.RunAsync(
                $"""
                Goals:
                {FormatGoals(state.Request.Goals)}

                Reviewer feedback:
                {state.CurrentFeedback}

                Code:
                {state.CurrentCode}

                Are all goals met? Reply with exactly one word: True or False.
                """,
                cancellationToken: cancellationToken);
            var decision = response.Text?.Trim() ?? string.Empty;
            var goalsMet = decision.ToLowerInvariant() switch
            {
                "true" => true,
                "false" => false,
                _ => throw new InvalidOperationException(
                    $"The goal judge must answer exactly True or False; received '{decision}'.")
            };

            writeOutput?.Invoke($"Goal judge: {goalsMet}");
            if (goalsMet)
            {
                writeOutput?.Invoke("All goals were reported met; stopping the bounded loop.");
            }

            var iterations = state.Iterations
                .Append(new GoalSettingIteration(number, state.CurrentCode, state.CurrentFeedback, goalsMet, decision))
                .ToArray();
            return state with
            {
                Iterations = iterations,
                IsComplete = goalsMet || iterations.Length >= state.Request.MaxIterations
            };
        }
    }

    [YieldsOutput(typeof(GoalSettingResult))]
    private sealed class CompletionExecutor() : Executor<GoalSettingProgress>("GoalCompletion")
    {
        public override ValueTask HandleAsync(
            GoalSettingProgress state, IWorkflowContext context, CancellationToken cancellationToken = default) =>
            context.YieldOutputAsync(new GoalSettingResult(state.Request, state.Iterations), cancellationToken);
    }
}
