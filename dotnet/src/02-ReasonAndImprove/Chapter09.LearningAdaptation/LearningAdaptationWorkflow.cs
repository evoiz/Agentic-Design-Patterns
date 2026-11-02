using Chapter09.LearningAdaptation.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter09.LearningAdaptation;

public static class LearningAdaptationWorkflow
{
    public const string TelemetrySourceName = "Chapter09.LearningAdaptation";
    public const string InitialInstructions =
        "You are a helpful customer-support assistant. Answer the customer's question briefly.";
    public const int MaximumInstructionLength = 4000;
    public const int MaximumIterations = 3;

    private const string CuratorInstructions = """
        You improve task instructions using evidence from a fixed evaluation contract.
        Return only a complete replacement instruction block, not an answer to any benchmark case.
        Preserve the original task and policy facts. Add only guidance needed to satisfy the listed
        requirements; do not weaken the evaluation criteria or invent policies.
        """;

    // Evaluate → curate a prompt from failures → re-evaluate; keep a candidate only if it scores higher.
    public static async Task<LearningAdaptationResult> RunAsync(
        IChatClient chatClient,
        string startingInstructions,
        int maxIterations = 1,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ValidateInstructions(startingInstructions);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxIterations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxIterations, MaximumIterations);

        writeOutput?.Invoke("--- Baseline evaluation ---");
        var baseline = await EvaluateAsync(chatClient, startingInstructions, writeOutput, cancellationToken);
        var best = baseline;
        var proposals = new List<PromptProposal>();
        var curator = CreateAgent(chatClient, "PromptCurator", CuratorInstructions);

        for (var iteration = 1; iteration <= maxIterations && !best.Passed; iteration++)
        {
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
            var proposedInstructions = response.Text?.Trim();
            ValidateInstructions(proposedInstructions);
            writeOutput?.Invoke($"\n--- Prompt candidate (iteration {iteration}) ---\n{proposedInstructions}");

            var candidate = await EvaluateAsync(chatClient, proposedInstructions!, writeOutput, cancellationToken);
            proposals.Add(new PromptProposal(iteration, candidate));
            writeOutput?.Invoke($"Candidate score: {candidate.PassedCases}/{candidate.TotalCases} cases passed.");
            if (candidate.PassedCases <= best.PassedCases)
            {
                break;
            }

            best = candidate;
        }

        return new LearningAdaptationResult(baseline, proposals, best);
    }

    private static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions) =>
        new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();

    // Deterministic benchmark: required/forbidden phrases decide pass/fail, not a model judge.
    private static async Task<PromptEvaluationResult> EvaluateAsync(
        IChatClient chatClient,
        string instructions,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var candidate = CreateAgent(chatClient, "PromptUnderEvaluation", instructions);
        var caseResults = new List<EvaluationCaseResult>(EvaluationBenchmark.Cases.Count);
        foreach (var benchmarkCase in EvaluationBenchmark.Cases)
        {
            // A fresh session per case keeps benchmark answers independent.
            var response = await candidate.RunAsync(benchmarkCase.Input, cancellationToken: cancellationToken);
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

    private static string FormatPhrases(IReadOnlyList<string> phrases) =>
        phrases.Count == 0 ? "none" : string.Join(", ", phrases);

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
