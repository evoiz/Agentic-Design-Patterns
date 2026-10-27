using System.Diagnostics;
using Chapter09.LearningAdaptation.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter09.LearningAdaptation;

public static class LearningAdaptationWorkflow
{
    public const string TelemetrySourceName = "Chapter09.LearningAdaptation";
    public const int MaximumInstructionLength = 4000;
    public const int MaximumIterations = 3;

    private static readonly ActivitySource ActivitySource = new(TelemetrySourceName);

    public static async Task<LearningAdaptationResult> RunAsync(
        IChatClient chatClient,
        string startingInstructions,
        int maxIterations = 1,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ValidateInstructions(startingInstructions);
        if (maxIterations is < 1 or > MaximumIterations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxIterations),
                maxIterations,
                $"The iteration budget must be between 1 and {MaximumIterations}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var activity = ActivitySource.StartActivity("learning-adaptation.run");
        activity?.SetTag("learning-adaptation.max_iterations", maxIterations);
        writeOutput?.Invoke("--- Baseline evaluation ---");
        var baseline = await EvaluateAsync(chatClient, startingInstructions, writeOutput, cancellationToken);
        var best = baseline;
        var proposals = new List<PromptProposal>();

        for (var iteration = 1; iteration <= maxIterations && !best.Passed; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writeOutput?.Invoke($"\n--- Prompt update proposal (iteration {iteration}) ---");
            var curator = CreateAgent(
                chatClient,
                "PromptCurator",
                """
                You improve task instructions using evidence from a fixed evaluation contract.
                Return only a complete replacement instruction block, not an answer to any benchmark case.
                Preserve the original task and policy facts. Add only guidance needed to satisfy the listed
                requirements; do not weaken the evaluation criteria or invent policies.
                """);
            var feedback = FormatFeedback(best);
            writeOutput?.Invoke($"\n--- Curated findings (iteration {iteration}) ---\n{feedback}");
            var response = await curator.RunAsync(
                $"""
                Current instructions:
                {best.Instructions}

                Evaluation findings:
                {feedback}

                Return only the revised instructions.
                """,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var proposedInstructions = response.Text?.Trim();
            ValidateInstructions(proposedInstructions);
            writeOutput?.Invoke(
                $"\n--- Prompt candidate (iteration {iteration}) ---\n{proposedInstructions}");

            var proposalEvaluation = await EvaluateAsync(
                chatClient,
                proposedInstructions!,
                writeOutput,
                cancellationToken);
            proposals.Add(new PromptProposal(iteration, proposalEvaluation));
            writeOutput?.Invoke(
                $"Candidate score: {proposalEvaluation.PassedCases}/{proposalEvaluation.TotalCases} cases passed.");

            if (proposalEvaluation.PassedCases > best.PassedCases)
            {
                best = proposalEvaluation;
            }
            else
            {
                break;
            }
        }

        activity?.SetTag("learning-adaptation.baseline_score", baseline.PassedCases);
        activity?.SetTag("learning-adaptation.best_score", best.PassedCases);
        activity?.SetTag("learning-adaptation.proposal_count", proposals.Count);
        return new LearningAdaptationResult(baseline, proposals, best);
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

    private static async Task<PromptEvaluationResult> EvaluateAsync(
        IChatClient chatClient,
        string instructions,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var caseResults = new List<EvaluationCaseResult>(EvaluationBenchmark.Cases.Count);
        foreach (var benchmarkCase in EvaluationBenchmark.Cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = CreateAgent(chatClient, "PromptUnderEvaluation", instructions);
            var response = await candidate.RunAsync(benchmarkCase.Input, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var answer = response.Text ?? string.Empty;
            var missing = benchmarkCase.RequiredPhrases
                .Where(phrase => !answer.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var forbidden = benchmarkCase.ForbiddenPhrases
                .Where(phrase => answer.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var result = new EvaluationCaseResult(
                benchmarkCase.Name,
                missing.Length == 0 && forbidden.Length == 0,
                missing,
                forbidden);
            caseResults.Add(result);

            writeOutput?.Invoke(
                $"{(result.Passed ? "PASS" : "FAIL")} {result.Name}" +
                (result.Passed ? string.Empty : $"; missing: {FormatPhrases(missing)}; forbidden: {FormatPhrases(forbidden)}"));
        }

        return new PromptEvaluationResult(
            instructions,
            caseResults.Count(result => result.Passed),
            caseResults.Count,
            caseResults);
    }

    private static string FormatFeedback(PromptEvaluationResult evaluation) =>
        string.Join(
            Environment.NewLine,
            evaluation.Cases
                .Where(result => !result.Passed)
                .Select(result =>
                    $"- {result.Name}: required phrases missing [{FormatPhrases(result.MissingPhrases)}]; " +
                    $"forbidden phrases present [{FormatPhrases(result.ForbiddenPhrases)}]."));

    private static string FormatPhrases(IEnumerable<string> phrases)
    {
        var values = phrases.ToArray();
        return values.Length == 0 ? "none" : string.Join(", ", values);
    }

    private static void ValidateInstructions(string? instructions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        if (instructions.Length > MaximumInstructionLength)
        {
            throw new ArgumentException(
                $"Instructions must be at most {MaximumInstructionLength} characters.",
                nameof(instructions));
        }
    }
}
