namespace Chapter21.ExplorationDiscovery.Models;

public sealed record ReviewerAssessment(
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Limitations,
    bool EthicalConcerns,
    int Originality,
    int Quality,
    int Clarity,
    int Significance,
    int Soundness,
    int Presentation,
    int Contribution,
    int Overall,
    int Confidence,
    string Decision);
