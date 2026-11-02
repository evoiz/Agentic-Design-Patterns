using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent;

public static partial class MultiAgentWorkflow
{
    private static Task<string> RunCrewAsync(
        IChatClient chatClient, string input, Action<string>? writeOutput, CancellationToken cancellationToken) =>
        RunCrewAsync(
            async (request, token) =>
                (await CreateCrewSpecialist(chatClient, "researcher").RunAsync(request, cancellationToken: token)).Text,
            async (request, token) =>
                (await CreateCrewSpecialist(chatClient, "writer").RunAsync(request, cancellationToken: token)).Text,
            input, writeOutput, cancellationToken);

    // The distributed companion hosts these same responsibilities at separate endpoints.
    public static AIAgent CreateCrewSpecialist(IChatClient chatClient, string role) => role switch
    {
        "researcher" => CreateAgent(chatClient, "SeniorResearchAnalyst", """
            Identify three important themes relevant to the user's topic and explain their practical impact.
            Use general model knowledge only; do not claim current web research or cite fabricated sources.
            Return concise notes for a writer.
            """),
        "writer" => CreateAgent(chatClient, "TechnicalContentWriter", """
            Write an engaging, accessible article of about 350 words based on the supplied research notes.
            Preserve uncertainty and do not introduce factual claims unsupported by those notes.
            Return the article only.
            """),
        _ => throw new ArgumentException("Select a crew specialist: researcher or writer.", nameof(role))
    };

    public static async Task<string> RunCrewAsync(
        Func<string, CancellationToken, Task<string>> researchTask,
        Func<string, CancellationToken, Task<string>> writingTask,
        string input,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(researchTask);
        ArgumentNullException.ThrowIfNull(writingTask);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        cancellationToken.ThrowIfCancellationRequested();
        var research = WriteResult("Crew research task", await researchTask(input, cancellationToken), writeOutput);
        cancellationToken.ThrowIfCancellationRequested();
        return WriteResult("Crew writing task", await writingTask(
            $"Topic: {input}\n\nResearch task output:\n{research}", cancellationToken), writeOutput);
    }
}
