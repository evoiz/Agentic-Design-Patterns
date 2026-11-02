using System.Text;

namespace Chapter21.ExplorationDiscovery.Models;

public sealed record ExplorationResult(
    ExplorationProposal Proposal,
    IReadOnlyList<ExplorationReview> Reviews,
    int ReviewRounds,
    bool Accepted)
{
    public string Format()
    {
        var output = new StringBuilder()
            .AppendLine($"Research question: {Proposal.ResearchQuestion}")
            .AppendLine($"Review rounds: {ReviewRounds}")
            .AppendLine($"Reviewer status: {(Accepted ? "accepted" : "needs human review")}")
            .AppendLine()
            .AppendLine("### Exploration plan");

        foreach (var (step, index) in Proposal.Plan.Select((step, index) => (step, index + 1)))
        {
            output.AppendLine($"{index}. {step}");
        }

        output.AppendLine().AppendLine("### Evidence to collect");
        foreach (var need in Proposal.EvidenceNeeds)
        {
            output.AppendLine($"- {need}");
        }

        output.AppendLine().AppendLine("### Risks and limitations");
        foreach (var risk in Proposal.Risks)
        {
            output.AppendLine($"- {risk}");
        }

        output.AppendLine().AppendLine("### Final reviewer assessments");
        foreach (var review in Reviews)
        {
            output.AppendLine(
                $"- {review.Reviewer}: {review.Assessment.Decision} " +
                $"(overall {review.Assessment.Overall}/10, confidence {review.Assessment.Confidence}/5)");
            output.AppendLine($"  Summary: {review.Assessment.Summary}");
            output.AppendLine($"  Strengths: {string.Join("; ", review.Assessment.Strengths)}");
            output.AppendLine($"  Weaknesses: {string.Join("; ", review.Assessment.Weaknesses)}");
            output.AppendLine($"  Questions: {string.Join("; ", review.Assessment.Questions)}");
            output.AppendLine($"  Limitations: {string.Join("; ", review.Assessment.Limitations)}");
            output.AppendLine($"  Ethical concerns: {review.Assessment.EthicalConcerns}");
        }

        output.AppendLine()
            .Append("This is an unverified proposal, not a literature review or experimental result.");
        return output.ToString();
    }
}
