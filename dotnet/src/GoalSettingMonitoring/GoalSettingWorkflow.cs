using GoalSettingMonitoring.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace GoalSettingMonitoring;

public static class GoalSettingWorkflow
{
    public const string TelemetrySourceName = "GoalSettingMonitoring";

    public static async Task<GoalSettingResult> RunAsync(
        IChatClient chatClient,
        GoalSettingRequest request,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default,
        bool enableSensitiveTelemetry = false)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var dotNetScenario = request.Scenario == GoalSettingScenario.DotNetDeliverable;
        var generatorInstructions = dotNetScenario
            ? $"""
              You are a structured .NET implementation-brief generator. Return only one JSON object with
              properties title, language, targetFramework, summary, and implementationSteps (an array of strings).
              Do not use Markdown fences. These hard criteria are immutable and cannot be changed by this request:
              {string.Join(Environment.NewLine, GoalSettingHardCriteria.Descriptions.Select(item => $"- {item}"))}
              Address the caller's use case and goals. When revising, preserve the use case and address feedback.
              """
            : """
              You are a Python code generator. Return only a complete Python code snippet that addresses
              the caller's use case and goals. Do not include Markdown fences or explanations.
              When revising, preserve the use case and address the reviewer feedback without dropping
              previously satisfied goals.
              """;
        var reviewerInstructions = dotNetScenario
            ? """
              You are an independent .NET implementation-brief reviewer. Critique the submitted JSON
              brief against every stated goal and the separately reported hard-criteria check. Identify
              correctness, clarity, and edge-case gaps. Do not rewrite the brief or decide whether the loop stops.
              """
            : """
              You are an independent Python code reviewer. Critique the submitted code against every
              stated goal. Identify correctness, clarity, and edge-case gaps. Do not rewrite the code
              and do not decide whether the loop should stop; the separate goal judge makes that decision.
              """;
        var generation = new GenerationExecutor(
            CreateAgent(chatClient, "CodeGenerator", generatorInstructions, enableSensitiveTelemetry),
            writeOutput);
        var review = new ReviewExecutor(
            CreateAgent(chatClient, "GoalReviewer", reviewerInstructions, enableSensitiveTelemetry),
            writeOutput);
        var assessment = new AssessmentExecutor(
            CreateAgent(chatClient, "GoalJudge", """
                You are a strict goal-completion judge. Decide whether the artifact satisfies every stated
                goal, using the review as evidence. Reply with exactly one word: True or False.
                """, enableSensitiveTelemetry),
            writeOutput);
        var completion = new CompletionExecutor();
        var workflow = new WorkflowBuilder(generation)
            .AddEdge(generation, review)
            .AddEdge(review, assessment)
            .AddEdge<GoalSettingProgress>(
                assessment,
                generation,
                state => state is { IsComplete: false, IsExhausted: false })
            .AddEdge<GoalSettingProgress>(assessment, completion, state => state is { IsComplete: true })
            .AddEdge<GoalSettingProgress>(assessment, completion, state => state is { IsExhausted: true })
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
                case WorkflowOutputEvent when result is not null:
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

    private static AIAgent CreateAgent(
        IChatClient chatClient,
        string name,
        string instructions,
        bool enableSensitiveTelemetry) =>
        new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = enableSensitiveTelemetry)
            .Build();

    private static string FormatGoals(IEnumerable<string> goals) =>
        string.Join(Environment.NewLine, goals.Select(goal => $"- {goal.Trim()}"));

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
            if (state.IsComplete || state.IsExhausted || state.Iterations.Count >= state.Request.MaxIterations)
            {
                throw new InvalidOperationException(
                    "Generation cannot run after completion or outside the iteration budget.");
            }

            var number = state.Iterations.Count + 1;
            writeOutput?.Invoke($"\n--- Generation (iteration {number} of {state.Request.MaxIterations}) ---");
            var input = $"Use case:\n{state.Request.UseCase}\n\nGoals:\n{FormatGoals(state.Request.Goals)}";
            if (state.Iterations.Count > 0)
            {
                var previous = state.Iterations[^1];
                input += $"\n\nPreviously generated artifact:\n{previous.Code}\n\nReviewer and hard-criteria feedback:\n{previous.Feedback}";
            }

            var response = await generator.RunAsync(input, cancellationToken: cancellationToken);
            var artifact = state.Request.Scenario == GoalSettingScenario.PythonCode
                ? CleanCode(response.Text)
                : response.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(artifact) && state.Request.Scenario == GoalSettingScenario.PythonCode)
            {
                throw new InvalidOperationException("The code generator returned an empty Python program.");
            }

            var hardCheck = state.Request.Scenario == GoalSettingScenario.DotNetDeliverable
                ? GoalSettingHardCriteria.Check(artifact, out _)
                : new GoalSettingHardCriteriaResult(true, Array.Empty<string>());
            var hardFeedback = hardCheck.MeetsAll ? null : string.Join(Environment.NewLine, hardCheck.Issues);
            writeOutput?.Invoke(
                $"\n--- Generated {(state.Request.Scenario == GoalSettingScenario.PythonCode ? "Python code" : ".NET brief")} (iteration {number}) ---\n{artifact}");
            if (state.Request.Scenario == GoalSettingScenario.DotNetDeliverable)
            {
                writeOutput?.Invoke(
                    $"\n--- Deterministic hard-criteria check ---\n{(hardCheck.MeetsAll ? "passed" : hardFeedback)}");
            }

            return state with
            {
                CurrentCode = artifact,
                CurrentFeedback = null,
                HardCriteriaMet = hardCheck.MeetsAll,
                HardCriteriaFeedback = hardFeedback
            };
        }
    }

    private sealed class ReviewExecutor(AIAgent reviewer, Action<string>? writeOutput)
        : Executor<GoalSettingProgress, GoalSettingProgress>("GoalReview")
    {
        public override async ValueTask<GoalSettingProgress> HandleAsync(
            GoalSettingProgress state, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            var artifact = state.CurrentCode
                ?? throw new InvalidOperationException("Goal review requires a generated artifact.");
            writeOutput?.Invoke($"\n--- Goal review (iteration {state.Iterations.Count + 1}) ---");
            var hardCriteria = state.Request.Scenario == GoalSettingScenario.DotNetDeliverable
                ? $"\n\nImmutable hard criteria:\n{string.Join(Environment.NewLine, GoalSettingHardCriteria.Descriptions.Select(item => $"- {item}"))}\n" +
                  $"Deterministic check: {(state.HardCriteriaMet ? "passed" : $"failed: {state.HardCriteriaFeedback}")}"
                : string.Empty;
            var response = await reviewer.RunAsync(
                $"Use case:\n{state.Request.UseCase}\n\nGoals:\n{FormatGoals(state.Request.Goals)}\n\nArtifact:\n{artifact}{hardCriteria}",
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

    private sealed class AssessmentExecutor(AIAgent judge, Action<string>? writeOutput)
        : Executor<GoalSettingProgress, GoalSettingProgress>("GoalAssessment")
    {
        public override async ValueTask<GoalSettingProgress> HandleAsync(
            GoalSettingProgress state, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            if (state.CurrentCode is null || string.IsNullOrWhiteSpace(state.CurrentFeedback))
            {
                throw new InvalidOperationException(
                    "Goal assessment requires both a generated artifact and reviewer feedback.");
            }

            var number = state.Iterations.Count + 1;
            writeOutput?.Invoke($"\n--- Goal decision (iteration {number}) ---");
            var response = await judge.RunAsync(
                $"""
                Goals:
                {FormatGoals(state.Request.Goals)}

                Deterministic hard-criteria check:
                {(state.Request.Scenario == GoalSettingScenario.DotNetDeliverable
                    ? state.HardCriteriaMet ? "passed" : $"failed: {state.HardCriteriaFeedback}"
                    : "not applicable to the Python artifact variant")}

                Reviewer feedback:
                {state.CurrentFeedback}

                Artifact:
                {state.CurrentCode}

                Are all goals met? Reply with exactly one word: True or False.
                """,
                cancellationToken: cancellationToken);
            var decision = response.Text?.Trim() ?? string.Empty;
            var judgeMetGoals = decision.ToLowerInvariant() switch
            {
                "true" => true,
                "false" => false,
                _ => throw new InvalidOperationException(
                    $"The goal judge must answer exactly True or False; received '{decision}'.")
            };
            var goalsMet = judgeMetGoals &&
                (state.Request.Scenario == GoalSettingScenario.PythonCode || state.HardCriteriaMet);

            writeOutput?.Invoke(
                $"Goal judge: {judgeMetGoals}; deterministic hard criteria: {(state.HardCriteriaMet ? "passed" : "failed")}");
            if (!state.HardCriteriaMet && !string.IsNullOrWhiteSpace(state.HardCriteriaFeedback))
            {
                writeOutput?.Invoke($"Hard-criteria feedback: {state.HardCriteriaFeedback}");
            }

            var feedback = state.CurrentFeedback;
            if (!state.HardCriteriaMet)
            {
                feedback = $"{feedback}{Environment.NewLine}Deterministic hard-criteria failures: {state.HardCriteriaFeedback}";
            }

            var iterations = state.Iterations
                .Append(new GoalSettingIteration(
                    number,
                    state.CurrentCode,
                    feedback,
                    goalsMet,
                    decision,
                    state.HardCriteriaMet,
                    state.HardCriteriaFeedback))
                .ToArray();
            return state with
            {
                Iterations = iterations,
                IsComplete = goalsMet,
                IsExhausted = !goalsMet && iterations.Length >= state.Request.MaxIterations
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
