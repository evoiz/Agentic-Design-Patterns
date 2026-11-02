using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter06.Planning;

public static class PlanningAgents
{
    public static (AIAgent Planner, AIAgent Writer) Create(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        var planner = CreateAgent(
            chatClient,
            "ArticlePlanner",
            """
            You are an expert technical writer and content strategist.
            Create a focused, ordered plan for a concise summary on the user's topic.
            Return exactly 4 to 5 actionable steps as structured output.
            Do not write the summary.
            """);
        var writer = CreateAgent(
            chatClient,
            "SummaryWriter",
            """
            You are an expert technical writer.
            Write a clear and engaging summary of about 200 words on the requested topic.
            Follow the supplied plan, preserve factual accuracy, and avoid unsupported specifics.
            Return only the summary text.
            """);

        return (planner, writer);
    }

    private static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions) =>
        new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(
                PlanningWorkflow.TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
}
