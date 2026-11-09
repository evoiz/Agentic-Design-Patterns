using AgenticDesignPatterns.Shared.Hosting;
using ExceptionHandling;
using Microsoft.Extensions.AI;

namespace ExceptionHandling.Hosted;

public sealed class ExceptionHandlingAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter12-exception-handling";
    public override string Description =>
        "Looks up a location precisely, retries bounded transient failures, and falls back to general-area information.";

    protected override Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
        ExceptionHandlingWorkflow.RunAsync(chatClient, input, cancellationToken: cancellationToken);
}
