using AgenticDesignPatterns.Shared;

namespace Chapter21.ExplorationDiscovery;

public static class DeepResearchDemo
{
    public static async Task RunAsync(
        FoundrySettings settings,
        string deploymentAlias,
        string topic,
        IReadOnlyList<string> domains,
        CancellationToken cancellationToken = default)
    {
        if (domains.Count == 0)
            throw new InvalidOperationException(
                "Foundry Deep Research requires one or more --domain values to bound public web search.");
        if (!settings.ModelDeployments.Keys.Any(alias =>
                string.Equals(alias, deploymentAlias, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                $"The Foundry Deep Research deployment alias '{deploymentAlias}' is not configured. " +
                "Add Foundry:ModelDeployments:deep-research to shared user-secrets or " +
                "Foundry__ModelDeployments__deep-research to the environment, then use " +
                "--research-deployment deep-research.");

        Console.WriteLine("\n=== Foundry Deep Research ===");
        await FoundryDeepResearch.RunAsync(
            settings, deploymentAlias, topic, Console.WriteLine,
            cancellationToken: cancellationToken,
            researchInstructions: """
                You are a research analyst exploring an open-ended question using public web sources.
                Search within the requested topic, compare source quality, and return a concise,
                citation-rich discovery report. Separate sourced findings from uncertainty.
                Search only within the configured allowed domains.
                Do not execute code, make external changes, or present hypotheses as experimental results.
                """,
            allowedDomains: domains);
    }
}
