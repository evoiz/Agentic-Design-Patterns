using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Microsoft.Agents.AI.Foundry.Hosting;

#pragma warning disable MAAI001 // Explicit isolation policies are currently experimental in the hosting SDK.

namespace Chapter11.GoalSettingMonitoring.Hosted;

public sealed class RequestIsolationKeyProvider : HostedSessionIsolationKeyProvider
{
    public override ValueTask<HostedSessionContext?> GetKeysAsync(
        ResponseContext context,
        CreateResponse request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ResponseId);
        return ValueTask.FromResult<HostedSessionContext?>(
            new HostedSessionContext($"request:{context.ResponseId}"));
    }
}
