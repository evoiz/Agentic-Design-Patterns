using AgenticDesignPatterns.Shared.Hosting;
using ConferenceDemos;
using Microsoft.Extensions.AI;

namespace ScheduleAgent.Hosted;

public sealed class ScheduleHostedAgent(IChatClient client) : StatelessWorkflowAgent
{
    public override string Name => "update-schedule";

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken)
    {
        if (input.Length > 4000)
        {
            throw new ArgumentException("Schedule questions must not exceed 4000 characters.", nameof(input));
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        return await ScheduleDemo.RunAsync(client, input, cancellationToken: deadline.Token);
    }
}
