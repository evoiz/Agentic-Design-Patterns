using Microsoft.Agents.AI.Workflows;

namespace ConferenceDemos;

public static class WelcomeWorkflowDemo
{
    public static async Task<string> RunAsync(
    string attendee = "Peter",
    CancellationToken cancellationToken = default,
    Action<WorkflowEvent>? onEvent = null)
    {
        Func<string, string> getProfile = attendee => $"{attendee}: .NET Developer interested in AI";
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

        await using Run run = await InProcessExecution.RunAsync(workflow, attendee);
        foreach (WorkflowEvent evt in run.NewEvents)
        {
            if (evt is ExecutorCompletedEvent executorComplete)
            {
                Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
            }
        }

        return "Completed workflow.";
        //return ConferenceWorkflow.RunAsync<string, string>(workflow, attendee, cancellationToken, onEvent);
    }

    private sealed class PrepareAttendee() : Executor<string, string>("GetAttendeeProfile")
    {
        public override ValueTask<string> HandleAsync(
            string message, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            return ValueTask.FromResult($"{message}: .NET Developer interested in AI");
        }
    }
}
