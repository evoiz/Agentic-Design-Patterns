using Microsoft.Agents.AI.Workflows;

namespace ConferenceDemos;

public static class WelcomeWorkflowDemo
{
    public static async Task<string> RunAsync(
        string attendee = "Peter",
        CancellationToken cancellationToken = default,
        Action<WorkflowEvent>? onEvent = null)
    {
        Func<string, string> getProfile = name =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return $"{name.Trim()}: .NET Developer interested in AI";
        };
        var profileExecutor = getProfile.BindAsExecutor("GetAttendeeProfile");

        Func<string, string> recommendSession =
            profile => profile.Contains("AI")
                ? "Building Agentic AI with Microsoft Foundry"
                : "What's New in .NET";
        var recommendationExecutor = recommendSession.BindAsExecutor("RecommendSession");


        Func<string, string> createMessage = session => $"Recommended session: {session}";
        var messageExecutor = createMessage.BindAsExecutor("CreateRecommendation");


        var workflow = new WorkflowBuilder(profileExecutor)
            .AddEdge(profileExecutor, recommendationExecutor)
            .AddEdge(recommendationExecutor, messageExecutor)
            .WithOutputFrom(messageExecutor)
            .Build();

        _ = await ConferenceWorkflow.RunAsync<string, string>(workflow, attendee, cancellationToken, evt =>
        {
            onEvent?.Invoke(evt);
            if (evt is ExecutorCompletedEvent executorComplete)
            {
                Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
            }
        });

        return "Completed workflow.";
    }
}
