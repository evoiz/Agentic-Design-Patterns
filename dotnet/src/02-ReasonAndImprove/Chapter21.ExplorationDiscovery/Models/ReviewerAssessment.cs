using System.Text.Json.Serialization;

namespace Chapter21.ExplorationDiscovery.Models;

public sealed record ReviewerAssessment(
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Limitations,
    [property: JsonRequired] bool EthicalConcerns,
    int Overall,
    int Confidence,
    string Decision);
