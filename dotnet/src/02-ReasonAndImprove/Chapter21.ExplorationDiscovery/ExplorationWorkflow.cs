using System.Text.Json;
using System.Text.RegularExpressions;
using Chapter21.ExplorationDiscovery.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter21.ExplorationDiscovery;

public static class ExplorationWorkflow
{
    public const string TelemetrySourceName = "Chapter21.ExplorationDiscovery";
    public const int MaxReviewRounds = 2;
    public const int MaxTopicLength = 4_000;
    public const int MinimumAcceptScore = 7;
    public static readonly TimeSpan TimeBudget = TimeSpan.FromMinutes(3);

    private static readonly (string Name, string Perspective)[] ReviewerPerspectives =
    [
        ("ExperimentReviewer", "Judge whether the proposed experiments can produce useful, interpretable insights."),
        ("ImpactReviewer", "Judge the potential importance, significance, and practical value of the research question."),
        ("NoveltyReviewer", "Judge whether the proposal identifies a plausible contribution and makes its uncertainty explicit.")
    ];

    public static async Task<ExplorationResult> RunAsync(
        IChatClient chatClient,
        string topic,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (topic.Length > MaxTopicLength)
        {
            throw new ArgumentException(
                $"Research topics must be at most {MaxTopicLength} characters.",
                nameof(topic));
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeBudget);
        var explorer = CreateAgent(
            chatClient,
            "ResearchExplorer",
            """
            You are a research postdoc helping formulate an investigation plan.
            Propose a focused, testable question and 3 to 5 ordered, unnumbered investigation steps.
            Identify evidence that would need to be collected, assumptions, and risks.
            Work from the caller's request and general model knowledge only. Do not claim to have
            read papers, searched the web, run code, collected data, or performed experiments.
            When revising, address the supplied reviewer feedback without inventing results.
            Return only the requested structured proposal.
            """);
        var reviewers = ReviewerPerspectives
            .Select(reviewer => (
                reviewer.Name,
                Agent: CreateAgent(
                    chatClient,
                    reviewer.Name,
                    $"""
                    You are a harsh but fair research reviewer. {reviewer.Perspective}
                    Assess only the submitted proposal; no experiment or literature search has been performed.
                    Give concise, proposal-specific strengths, weaknesses, questions, limitations, and ethical concerns.
                    Return an overall score from 1 to 10, confidence from 1 to 5, and Decision exactly "Accept" or "Reject".
                    Accept only a clear, feasible proposal with testable steps, relevant evidence needs, and explicit risks.
                    Do not include private chain-of-thought; provide concise review conclusions.
                    Return an empty array for any list field with no items.
                    """)))
            .ToArray();

        var revisionContext = "No prior draft or reviewer feedback.";
        ExplorationProposal? finalProposal = null;
        IReadOnlyList<ExplorationReview>? finalReviews = null;
        try
        {
            for (var round = 1; round <= MaxReviewRounds; round++)
            {
                var response = await explorer.RunAsync<ExplorationProposal>(
                    $"""
                    Research topic: {topic}

                    Review round: {round} of {MaxReviewRounds}
                    Previous draft and feedback:
                    {revisionContext}

                    Produce or revise the research exploration proposal.
                    """,
                    cancellationToken: budget.Token);
                var proposal = response.Result
                    ?? throw new InvalidOperationException("The research explorer returned no structured proposal.");
                ValidateProposal(proposal);
                proposal = proposal with
                {
                    Plan = proposal.Plan.Select(NormalizePlanStep).ToArray()
                };
                ValidateProposal(proposal);
                finalProposal = proposal;

                writeOutput?.Invoke($"\n--- Exploration proposal (round {round} of {MaxReviewRounds}) ---");
                writeOutput?.Invoke(FormatProposal(proposal));

                var reviewTasks = reviewers.Select(async reviewer =>
                {
                    var reviewResponse = await reviewer.Agent.RunAsync<ReviewerAssessment>(
                        $"""
                        Research question: {topic}

                        Proposal:
                        {JsonSerializer.Serialize(proposal)}
                        """,
                        cancellationToken: budget.Token);
                    var assessment = reviewResponse.Result
                        ?? throw new InvalidOperationException(
                            $"Reviewer '{reviewer.Name}' returned no structured assessment.");
                    ValidateAssessment(reviewer.Name, assessment);
                    return new ExplorationReview(reviewer.Name, assessment);
                });

                var assessments = await Task.WhenAll(reviewTasks);
                finalReviews = assessments;
                writeOutput?.Invoke("\n--- Independent reviewer panel ---");
                foreach (var review in assessments)
                {
                    writeOutput?.Invoke(
                        $"{review.Reviewer}: {review.Assessment.Decision}, " +
                        $"overall {review.Assessment.Overall}/10 — {review.Assessment.Summary}");
                    foreach (var weakness in review.Assessment.Weaknesses)
                    {
                        writeOutput?.Invoke($"  Gap: {weakness}");
                    }
                }

                var accepted = assessments.All(review =>
                    string.Equals(review.Assessment.Decision, "Accept", StringComparison.OrdinalIgnoreCase) &&
                    review.Assessment.Overall >= MinimumAcceptScore &&
                    !review.Assessment.EthicalConcerns);
                if (accepted)
                {
                    return new ExplorationResult(proposal, assessments, round, Accepted: true);
                }

                revisionContext = $"""
                    Current proposal:
                    {JsonSerializer.Serialize(proposal)}

                    Reviewer feedback:
                    {JsonSerializer.Serialize(assessments)}
                    """;
                if (round < MaxReviewRounds)
                {
                    writeOutput?.Invoke("--- Completion check: revise within the remaining budget ---");
                }
            }

            return new ExplorationResult(
                finalProposal
                    ?? throw new InvalidOperationException("The bounded exploration loop produced no final proposal."),
                finalReviews
                    ?? throw new InvalidOperationException("The bounded exploration loop produced no final reviews."),
                MaxReviewRounds,
                Accepted: false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The exploration request exceeded its {TimeBudget.TotalMinutes:0}-minute time budget.",
                exception);
        }
    }

    private static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions) =>
        new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();

    private static void ValidateProposal(ExplorationProposal proposal)
    {
        if (string.IsNullOrWhiteSpace(proposal.ResearchQuestion) ||
            proposal.Plan is null ||
            proposal.Plan.Count is < 3 or > 5 ||
            proposal.Plan.Any(string.IsNullOrWhiteSpace) ||
            proposal.EvidenceNeeds is null ||
            proposal.EvidenceNeeds.Count == 0 ||
            proposal.EvidenceNeeds.Any(string.IsNullOrWhiteSpace) ||
            proposal.Risks is null ||
            proposal.Risks.Count == 0 ||
            proposal.Risks.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "The research explorer must return a question, three to five plan steps, evidence needs, and risks.");
        }
    }

    private static void ValidateAssessment(string reviewer, ReviewerAssessment assessment)
    {
        IReadOnlyList<string>?[] lists =
            [assessment.Strengths, assessment.Weaknesses, assessment.Questions, assessment.Limitations];
        if (string.IsNullOrWhiteSpace(assessment.Summary) ||
            lists.Any(list => list is null || list.Any(string.IsNullOrWhiteSpace)) ||
            assessment.Overall is < 1 or > 10 ||
            assessment.Confidence is < 1 or > 5 ||
            !(string.Equals(assessment.Decision, "Accept", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(assessment.Decision, "Reject", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Reviewer '{reviewer}' must return a summary, non-null review lists, " +
                "overall score (1-10), confidence (1-5), and an Accept or Reject decision.");
        }
    }

    private static string FormatProposal(ExplorationProposal proposal) =>
        $"""
        Research question: {proposal.ResearchQuestion}
        Plan:
        {string.Join(Environment.NewLine, proposal.Plan.Select((step, index) => $"{index + 1}. {step}"))}
        Evidence needs: {string.Join("; ", proposal.EvidenceNeeds)}
        Risks: {string.Join("; ", proposal.Risks)}
        """;

    private static string NormalizePlanStep(string step) =>
        Regex.Replace(step.Trim(), @"^(?:\d+\.\s+)+", string.Empty);
}
