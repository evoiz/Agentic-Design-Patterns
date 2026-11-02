namespace Chapter16.ResourceOptimization.Models;

public sealed record OptimizationResult(
    OptimizationCategory Category,
    string ModelDeployment,
    string Answer,
    int WordCount,
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens,
    IReadOnlyList<GoogleSearchResult> SearchResults);
