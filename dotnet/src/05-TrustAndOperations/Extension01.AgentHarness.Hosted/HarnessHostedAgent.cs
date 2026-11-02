using System.Text.Json;
using AgenticDesignPatterns.Shared.Hosting;
using Microsoft.Extensions.AI;

namespace Extension01.AgentHarness.Hosted;

public sealed class HarnessHostedAgent(IChatClient chatClient, bool enableSensitiveTelemetry = false)
    : StatelessWorkflowAgent
{
    public override string Name => "extension01-agent-harness";
    public override string Description => "Bounded read-only support triage with session todos, evidence, and explicit completion status.";

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await HarnessRunner.RunAsync(
            chatClient, input, enableSensitiveTelemetry: enableSensitiveTelemetry, cancellationToken: cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
