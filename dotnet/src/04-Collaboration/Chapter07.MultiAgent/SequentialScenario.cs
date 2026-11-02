using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent;

public static partial class MultiAgentWorkflow
{
    private static async Task<string> RunSequentialAsync(
        IChatClient chatClient, string input, Action<string>? writeOutput, CancellationToken cancellationToken)
    {
        var researcher = CreateAgent(chatClient, "Researcher", """
            Summarize relevant background for the requested topic in three concise points.
            Work from general model knowledge only. Do not claim live web research or invent sources.
            """);
        var writer = CreateAgent(chatClient, "Writer", """
            Turn the supplied research notes into a clear, concise explanation.
            Use only the supplied notes and distinguish uncertainty from established facts.
            """);

        var research = await researcher.RunAsync(input, cancellationToken: cancellationToken);
        var notes = WriteResult("Researcher output", research.Text, writeOutput);
        var response = await writer.RunAsync(
            $"Original request: {input}\n\nResearch notes:\n{notes}", cancellationToken: cancellationToken);
        return WriteResult("Writer output", response.Text, writeOutput);
    }
}
