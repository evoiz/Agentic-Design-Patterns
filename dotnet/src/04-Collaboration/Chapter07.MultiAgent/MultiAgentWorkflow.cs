using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter07.MultiAgent;

public enum MultiAgentScenario { Sequential, Parallel, Loop, Coordinator, AgentTool, Crew }

public static partial class MultiAgentWorkflow
{
    public const string TelemetrySourceName = "Chapter07.MultiAgent";
    public const int MaxLoopIterations = 3;

    public static Task<string> RunAsync(
        IChatClient chatClient,
        MultiAgentScenario scenario,
        string input,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        cancellationToken.ThrowIfCancellationRequested();
        return scenario switch
        {
            MultiAgentScenario.Sequential => RunSequentialAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.Parallel => RunParallelAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.Loop => RunLoopAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.Coordinator => RunCoordinatorAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.AgentTool => RunAgentToolAsync(chatClient, input, writeOutput, cancellationToken),
            MultiAgentScenario.Crew => RunCrewAsync(chatClient, input, writeOutput, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown multi-agent scenario.")
        };
    }

    public static bool TryParseScenario(string? value, out MultiAgentScenario scenario)
    {
        var normalized = value?.Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse(normalized, ignoreCase: true, out scenario) && Enum.IsDefined(scenario);
    }

    private static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions, params AITool[] tools) =>
        new ChatClientAgent(chatClient, instructions, name: name, tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();

    private static string RequireText(string? value, string errorMessage) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException(errorMessage);

    private static string WriteResult(string heading, string? text, Action<string>? writeOutput)
    {
        var result = RequireText(text, $"{heading} returned an empty response.");
        writeOutput?.Invoke($"\n--- {heading} ---\n{result}");
        return result;
    }
}
