using Chapter16.ResourceOptimization.Models;
using Microsoft.Extensions.AI;

namespace Chapter16.ResourceOptimization.Demos;

public static class ResourceOptimizationDemos
{
    public static async Task RunComplexityAsync(
        IChatClient classifierClient,
        IReadOnlyDictionary<OptimizationCategory, OptimizationModelClient> modelClients)
    {
        var requests = new[]
        {
            "What is the capital of Australia?",
            "Compare the trade-offs of using solar, wind, and hydroelectric power for a remote community. Consider seasonal availability, storage needs, installation cost, maintenance, and how the options could work together."
        };

        foreach (var request in requests)
        {
            Console.WriteLine($"\n>>> User request: {request}");
            var result = await ResourceOptimizationWorkflow.RunAsync(
                request,
                RoutingStrategy.Complexity,
                classifierClient,
                modelClients,
                writeOutput: Console.WriteLine);
            PrintFinalResult(result);
        }
    }

    public static async Task RunIntentClassificationAsync(
        IChatClient classifierClient,
        IReadOnlyDictionary<OptimizationCategory, OptimizationModelClient> modelClients,
        IGoogleCustomSearch? searchClient)
    {
        var requests = new List<string>
        {
            "What is the capital of Australia?",
            "Explain how quantum computing could affect public-key cryptography, including the assumptions and likely transition challenges."
        };
        if (searchClient is null)
        {
            Console.WriteLine(
                "Current-information sample skipped: configure Google Custom Search credentials to enable it.");
        }
        else
        {
            requests.Add("When does the Australian Open 2026 start? Give me the full date.");
        }

        foreach (var request in requests)
        {
            Console.WriteLine($"\n>>> User request: {request}");
            var result = await ResourceOptimizationWorkflow.RunAsync(
                request,
                RoutingStrategy.IntentClassification,
                classifierClient,
                modelClients,
                searchClient,
                Console.WriteLine);
            PrintFinalResult(result);
        }
    }

    private static void PrintFinalResult(OptimizationResult result)
    {
        Console.WriteLine("\n--- Final optimized response ---");
        Console.WriteLine($"Classification: {result.Category}");
        Console.WriteLine($"Model deployment: {result.ModelDeployment}");
        Console.WriteLine(result.Answer);
    }
}
