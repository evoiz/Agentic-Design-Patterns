using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter04.Reflection;

public enum ReflectionScenario
{
    FactCheck,
    ProductDescription,
    IterativeCodeReview
}

public static class ReflectionDemos
{
    public const string FactorialSampleTask = """
        Write a Python function named calculate_factorial that accepts one integer n and returns n!.
        Include a clear docstring. The factorial of 0 is 1. Raise ValueError for negative input.
        Output only the Python code.
        """;

    public static (AIAgent Generator, AIAgent Verifier) CreateAgents(IChatClient chatClient, ReflectionScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        var (generationInstructions, reviewInstructions) = scenario switch
        {
            ReflectionScenario.FactCheck => (
                "Write a short, informative paragraph about the user's subject. Do not invent citations or claim external research.",
                """
                You are an independent, meticulous fact-checker. Review the paragraph against the original task using
                your knowledge only; you have no browsing tool, so do not claim external verification.
                Accept only when the claims are sufficiently accurate and uncertainty is appropriately qualified.
                Otherwise request revision with specific questionable claims and concrete corrections.
                """),
            ReflectionScenario.ProductDescription => (
                // Deliberately misleading: the writer is nudged toward long, lifestyle-heavy copy and is not told
                // the house style that the verifier enforces, so the first draft almost always needs a feedback round.
                """
                Write a rich, evocative product description for the product described by the user.
                Paint a lifestyle scene and build excitement. Use only the supplied product details.
                """,
                """
                You review a product description and enforce the brand's house style.
                Accept only when ALL hold:
                1. At most 40 words in total.
                2. The final sentence is exactly: "Made for the way you live."
                3. Every claim is supported by the supplied product details; no invented attributes.
                4. It reads clearly and appealingly.
                Count the words yourself. If any rule fails, request revision, quote the exact rule(s) violated
                (including the required closing sentence verbatim), and give concrete fixes.
                Accept as soon as all rules are met; do not demand changes just to keep the loop running.
                """),
            ReflectionScenario.IterativeCodeReview => (
                "You are an expert Python programmer. Follow the user's task exactly and output only complete Python code.",
                """
                You are an independent senior Python code reviewer. Compare the code with every original requirement,
                including specified edge cases and documentation. Do not add unrelated requirements or claim code execution.
                Accept only when all requested requirements are visibly satisfied.
                Otherwise request revision with concrete findings and actionable corrections.
                """),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown reflection scenario.")
        };

        return (
            CreateAgent(chatClient, "DraftWriter", generationInstructions),
            CreateAgent(chatClient, "IndependentVerifier", reviewInstructions + """

                Return structured output with decision exactly "Accept" or "Revise" and nonempty reasoning.
                Explain acceptance, or provide actionable corrections for revision.
                """));
    }

    public static async Task<string> RunAsync(
        IChatClient chatClient,
        ReflectionScenario scenario,
        string input,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        var (generator, verifier) = CreateAgents(chatClient, scenario);
        var result = await ReflectionWorkflow.RunAsync(
            generator, verifier, input, writeOutput: writeOutput, cancellationToken: cancellationToken);
        var output = FormatResult(result);
        writeOutput?.Invoke($"\n--- Final result ---\n{output}");
        return output;
    }

    public static string FormatResult(ReflectionResult result) =>
        $"""
        Outcome: {result.Outcome}
        Rounds: {result.RoundsUsed} of {result.MaxRounds}
        {(result.Outcome == ReflectionOutcome.Accepted ? "The verifier accepted this draft." : "The round limit was reached; this draft was not accepted.")}

        Draft:
        {result.Draft}

        Review: {result.Review.Decision}
        Reasoning: {result.Review.Reasoning}
        """;

    private static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions) =>
        new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(
                ReflectionWorkflow.TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
}
