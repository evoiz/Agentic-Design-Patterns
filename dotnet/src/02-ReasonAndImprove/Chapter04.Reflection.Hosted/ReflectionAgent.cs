using AgenticDesignPatterns.Shared.Hosting;
using Chapter04.Reflection;
using Microsoft.Extensions.AI;

namespace Chapter04.Reflection.Hosted;

public sealed class ReflectionAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter04-reflection";
    public override string Description =>
        "Generates content, obtains an independent structured review, and refines within a fixed limit.";

    protected override Task<string> RunAsync(string request, CancellationToken cancellationToken)
    {
        var (scenario, input) = ParseRequest(request);
        return ReflectionDemos.RunAsync(
            chatClient,
            scenario,
            input,
            cancellationToken: cancellationToken);
    }

    private static (ReflectionScenario Scenario, string Input) ParseRequest(string request)
    {
        const string factCheckPrefix = "fact-check:";
        const string productPrefix = "product:";
        const string codeReviewPrefix = "code-review:";

        if (request.StartsWith(factCheckPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (ReflectionScenario.FactCheck, GetInput(request, factCheckPrefix));
        }

        if (request.StartsWith(productPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (ReflectionScenario.ProductDescription, GetInput(request, productPrefix));
        }

        if (request.StartsWith(codeReviewPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (ReflectionScenario.IterativeCodeReview, GetInput(request, codeReviewPrefix));
        }

        return (ReflectionScenario.ProductDescription, request);
    }

    private static string GetInput(string request, string prefix)
    {
        var input = request[prefix.Length..].Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return input;
    }

}
