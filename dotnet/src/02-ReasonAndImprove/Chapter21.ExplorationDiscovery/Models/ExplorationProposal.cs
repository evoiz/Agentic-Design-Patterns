namespace Chapter21.ExplorationDiscovery.Models;

public sealed record ExplorationProposal(
    string ResearchQuestion,
    IReadOnlyList<string> Plan,
    IReadOnlyList<string> EvidenceNeeds,
    IReadOnlyList<string> Risks);
