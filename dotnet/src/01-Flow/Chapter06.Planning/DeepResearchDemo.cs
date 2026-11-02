using AgenticDesignPatterns.Shared;

namespace Chapter06.Planning;

internal static class DeepResearchDemo
{
    public static Task RunAsync(FoundrySettings settings, string deploymentAlias, string question)
    {
        if (!settings.ModelDeployments.Keys.Any(alias =>
                string.Equals(alias, deploymentAlias, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"The Deep Research deployment alias '{deploymentAlias}' is not configured. " +
                "Add Foundry:ModelDeployments:deep-research to shared user-secrets or " +
                "Foundry__ModelDeployments__deep-research to the environment, then run with " +
                "--research-deployment deep-research.");
        }

        Console.WriteLine("\n=== Foundry Deep Research ===");
        return FoundryDeepResearch.RunAsync(settings, deploymentAlias, question, Console.WriteLine);
    }
}
