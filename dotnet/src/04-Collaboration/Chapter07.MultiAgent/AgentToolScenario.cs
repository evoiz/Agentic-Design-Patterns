using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent;

public static partial class MultiAgentWorkflow
{
    private static async Task<string> RunAgentToolAsync(
        IChatClient chatClient, string input, Action<string>? writeOutput, CancellationToken cancellationToken)
    {
        var imageTool = AIFunctionFactory.Create(
            (string prompt) => $"Mock image asset for prompt: {prompt.Trim()} (no image service is connected).",
            name: "generate_image",
            description: "Returns a mock image-generation result for the supplied visual prompt.");
        var imageGenerator = CreateAgent(chatClient, "ImageGen", """
            You are an image-generation specialist. Pass the full visual prompt to generate_image.
            Explain that the returned asset is mock data; do not imply that a real image was generated.
            """, imageTool);
        var artist = CreateAgent(chatClient, "Artist", """
            Invent a concise, vivid visual prompt for the user's request.
            Then call the ImageGen specialist with that prompt and report its result.
            """, imageGenerator.AsAIFunction());
        var response = await artist.RunAsync(input, cancellationToken: cancellationToken);
        return WriteResult("Artist and image specialist result", response.Text, writeOutput);
    }
}
