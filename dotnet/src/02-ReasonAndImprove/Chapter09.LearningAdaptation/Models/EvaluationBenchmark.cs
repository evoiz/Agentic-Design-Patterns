namespace Chapter09.LearningAdaptation.Models;

public sealed record EvaluationBenchmarkCase(
    string Name,
    string Input,
    IReadOnlyList<string> RequiredPhrases,
    IReadOnlyList<string> ForbiddenPhrases);

public static class EvaluationBenchmark
{
    public static IReadOnlyList<EvaluationBenchmarkCase> Cases { get; } =
    [
        new(
            "Eligible sealed return",
            """
            Policy: Standard items can be returned within 30 days of delivery. Refunds are issued within five business days after the return is received.
            Customer: My sealed order arrived four days ago. Can I return it for a refund?
            """,
            ["eligible", "five business days"],
            ["guaranteed"]),
        new(
            "Late return",
            """
            Policy: Standard items can be returned within 30 days of delivery. Refunds are issued within five business days after the return is received.
            Customer: My order arrived 45 days ago. Can I still receive a refund?
            """,
            ["not eligible", "30 days"],
            ["refund approved"]),
        new(
            "Damaged delivery",
            """
            Policy: For a damaged delivery, support needs the order number and a photo of the damage to investigate.
            Customer: My package arrived damaged. What information should I send?
            """,
            ["order number", "photo"],
            [])
    ];
}
