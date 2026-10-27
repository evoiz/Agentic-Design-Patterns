using System.Diagnostics;
using Chapter11.GoalSettingMonitoring.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter11.GoalSettingMonitoring;

public static class GoalSettingWorkflow
{
    public const string TelemetrySourceName = "Chapter11.GoalSettingMonitoring";

    private static readonly ActivitySource ActivitySource = new(TelemetrySourceName);

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

        using var activity = ActivitySource.StartActivity("goal-setting.run");
        activity?.SetTag("goal-setting.max_iterations", request.MaxIterations);
        activity?.SetTag("goal-setting.goal_count", request.Goals.Count);

        var generator = CreateAgent(
            chatClient,
            "CodeGenerator",
            """
            You are a Python code generator. Return only a complete Python code snippet that addresses
            the caller's use case and goals. Do not include Markdown fences or explanations.
            When revising, preserve the use case and address the reviewer feedback without dropping
            previously satisfied goals.
            """);
        var reviewer = CreateAgent(
            chatClient,
            "GoalReviewer",
            """
            You are an independent Python code reviewer. Critique the submitted code against every
            stated goal. Identify correctness, clarity, and edge-case gaps. Do not rewrite the code
            and do not decide whether the loop should stop; the separate goal judge makes that decision.
            """);
        var judge = CreateAgent(
            chatClient,
            "GoalJudge",
            """
            You are a strict goal-completion judge. Decide whether the code satisfies every stated
            goal, using the review as evidence. Reply with exactly one word: True or False.
            """);

        var generation = new GenerationExecutor(generator, writeOutput);
        var review = new ReviewExecutor(reviewer, writeOutput);
        var assessment = new AssessmentExecutor(judge, writeOutput);
        var decision = new DecisionExecutor(writeOutput);
        var completion = new CompletionExecutor();
        var workflow = new WorkflowBuilder(generation)
            .AddEdge(generation, review)
            .AddEdge(review, assessment)
            .AddEdge(assessment, decision)
            .AddEdge<GoalSettingProgress>(decision, generation, state => state is { IsComplete: false })
            .AddEdge<GoalSettingProgress>(decision, completion, state => state is { IsComplete: true })
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
                case WorkflowOutputEvent output:
                    if (result is not null)
                    {
                        throw new InvalidOperationException(
                            "The goal-setting workflow produced multiple terminal results.");
                    }

                    result = output.As<GoalSettingResult>()
                        ?? throw new InvalidOperationException(
                            "The goal-setting workflow produced an invalid terminal result.");
                    break;
            }
        }

        if (result is null)
        {
            throw new InvalidOperationException(
                "The goal-setting workflow completed without a terminal result.");
        }

        activity?.SetTag("goal-setting.iterations_used", result.Iterations.Count);
        activity?.SetTag("goal-setting.goals_met", result.GoalsMet);
        return result;
    }

    public static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);

        return new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
    }

    private static string FormatGenerationInput(GoalSettingProgress state)
    {
        var input = $"""
            Use case:
            {state.Request.UseCase}

            Goals:
            {FormatGoals(state.Request.Goals)}
            """;

        if (state.Iterations.Count > 0)
        {
            var previous = state.Iterations[^1];
            input += $"""

                Previously generated code:
                {previous.Code}

                Reviewer feedback:
                {previous.Feedback}
                """;
        }

        return input;
    }

    private static string FormatReviewInput(GoalSettingRequest request, string code) =>
        $"""
        Use case:
        {request.UseCase}

        Goals:
        {FormatGoals(request.Goals)}

        Code:
        {code}
        """;

    private static string FormatJudgeInput(
        GoalSettingRequest request,
        string code,
        string feedback) =>
        $"""
        Goals:
        {FormatGoals(request.Goals)}

        Reviewer feedback:
        {feedback}

        Code:
        {code}

        Are all goals met? Reply with exactly one word: True or False.
        """;

    private static string FormatGoals(IEnumerable<string> goals) =>
        string.Join(Environment.NewLine, goals.Select(goal => $"- {goal.Trim()}"));

    private static string CleanCode(string? text)
    {
        var lines = text?.Trim().Split('\n').ToList() ?? [];
        if (lines.Count > 0 && lines[0].Trim().StartsWith("```", StringComparison.Ordinal))
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
            GoalSettingProgress state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.IsComplete || state.Iterations.Count >= state.Request.MaxIterations)
            {
                throw new InvalidOperationException(
                    "Code generation cannot run after goal completion or outside the iteration budget.");
            }

            var number = state.Iterations.Count + 1;
            writeOutput?.Invoke($"\n--- Code generation (iteration {number} of {state.Request.MaxIterations}) ---");
            var response = await generator.RunAsync(
                FormatGenerationInput(state),
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
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
            GoalSettingProgress state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(state.CurrentCode))
            {
                throw new InvalidOperationException("Goal review requires generated code.");
            }

            var number = state.Iterations.Count + 1;
            writeOutput?.Invoke($"\n--- Goal review (iteration {number}) ---");
            var response = await reviewer.RunAsync(
                FormatReviewInput(state.Request, state.CurrentCode),
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var feedback = response.Text?.Trim();
            if (string.IsNullOrWhiteSpace(feedback))
            {
                throw new InvalidOperationException("The goal reviewer returned empty feedback.");
            }

            writeOutput?.Invoke(feedback);
            return state with { CurrentFeedback = feedback };
        }
    }

    private sealed class AssessmentExecutor(AIAgent judge, Action<string>? writeOutput)
        : Executor<GoalSettingProgress, GoalSettingProgress>("GoalAssessment")
    {
        public override async ValueTask<GoalSettingProgress> HandleAsync(
            GoalSettingProgress state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(state.CurrentCode) ||
                string.IsNullOrWhiteSpace(state.CurrentFeedback))
            {
                throw new InvalidOperationException(
                    "Goal assessment requires both generated code and reviewer feedback.");
            }

            var number = state.Iterations.Count + 1;
            writeOutput?.Invoke($"\n--- Goal decision (iteration {number}) ---");
            var response = await judge.RunAsync(
                FormatJudgeInput(state.Request, state.CurrentCode, state.CurrentFeedback),
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var decision = response.Text?.Trim();
            var goalsMet = decision switch
            {
                { } value when string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) => true,
                { } value when string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) => false,
                _ => throw new InvalidOperationException(
                    $"The goal judge must answer exactly True or False; received '{decision}'.")
            };

            writeOutput?.Invoke($"Goal judge: {goalsMet}");
            var iteration = new GoalSettingIteration(
                number,
                state.CurrentCode,
                state.CurrentFeedback,
                goalsMet,
                decision!);
            return state with { Iterations = [.. state.Iterations, iteration] };
        }
    }

    private sealed class DecisionExecutor(Action<string>? writeOutput)
        : Executor<GoalSettingProgress, GoalSettingProgress>("GoalDecision")
    {
        public override ValueTask<GoalSettingProgress> HandleAsync(
            GoalSettingProgress state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.Iterations.Count == 0)
            {
                throw new InvalidOperationException("Goal completion cannot be decided before an assessment.");
            }

            var latest = state.Iterations[^1];
            var isComplete = latest.GoalsMet || state.Iterations.Count >= state.Request.MaxIterations;
            if (latest.GoalsMet)
            {
                writeOutput?.Invoke("All goals were reported met; stopping the bounded loop.");
            }

            return ValueTask.FromResult(state with { IsComplete = isComplete });
        }
    }

    [YieldsOutput(typeof(GoalSettingResult))]
    private sealed class CompletionExecutor() : Executor<GoalSettingProgress>("GoalCompletion")
    {
        public override async ValueTask HandleAsync(
            GoalSettingProgress state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            if (!state.IsComplete || state.Iterations.Count == 0)
            {
                throw new InvalidOperationException("Goal-setting cannot complete before its stopping condition.");
            }

            await context.YieldOutputAsync(
                new GoalSettingResult(state.Request, state.Iterations),
                cancellationToken);
        }
    }
}
