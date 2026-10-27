using Microsoft.Extensions.AI;

namespace Chapter01.PromptChaining.Demos;

internal static class MarketTrendsDemo
{
    private const string ResearchNotes =
        "Consumer research: 73% of consumers prefer to do business with brands that use personal information to make their shopping experiences more relevant. " +
        "Product sales analysis: Sales of products with ESG-related claims grew 28% over the last five years, compared to 20% for products without.";

    public static Task RunAsync(IChatClient chatClient) =>
        PromptChainRunner.RunAsync(
            chatClient,
            ResearchNotes,
            "MarketTrendExtractor",
            "Identify distinct market trends supported by the user's notes. For each trend, preserve its quantitative evidence accurately. Do not add claims or infer causation.",
            "MarketTrendJsonFormatter",
            "Transform the identified trends into valid JSON matching this schema: {\"trends\":[{\"trend_name\":\"...\",\"supporting_data\":\"...\"}]}. Preserve the evidence and percentages. Return only JSON, with no Markdown fences or commentary.");
}
