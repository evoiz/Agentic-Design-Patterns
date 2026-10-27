using Microsoft.Extensions.AI;

namespace Chapter01.PromptChaining.Demos;

internal static class TechnicalSpecificationsDemo
{
    public static Task RunAsync(IChatClient chatClient) =>
        PromptChainRunner.RunAsync(
            chatClient,
            "The new laptop model features a 3.5 GHz octa-core processor, 16GB of RAM, and a 1TB NVMe SSD.",
            "SpecificationExtractor",
            "Extract only the technical specifications explicitly stated in the user's text. Preserve values and units. Do not infer missing facts.",
            "SpecificationJsonFormatter",
            "Transform the extracted specifications into a valid JSON object with exactly these keys: \"cpu\", \"memory\", and \"storage\". Preserve the source values and units. Return only JSON, with no Markdown fences or commentary.");
}
