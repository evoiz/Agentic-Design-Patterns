using AgenticDesignPatterns.Shared;
using AgentHarness;
using Xunit;

namespace AgenticDesignPatterns.Foundry.SmokeTests;

[Trait("Category", "LiveFoundry")]
public sealed class AgentHarnessSmokeTests
{
    [Fact(Timeout = 240000)]
    public async Task Enterprise_agent_harness_completes_a_live_read_only_investigation()
    {
        var settings = FoundryChatClientFactory.LoadSettings();
        using var client = FoundryChatClientFactory.CreateChatClient(
            settings, Environment.GetEnvironmentVariable("FOUNDRY_TEST_DEPLOYMENT_ALIAS"));
        var result = await HarnessRunner.RunAsync(client,
            "Investigate checkout latency. Inspect its health and runbook, then recommend next steps.");

        Assert.True(result.Status == "completed", result.IncompleteReason);
        Assert.InRange(result.ModelCalls, 1, HarnessRunner.MaximumModelCalls);
        Assert.NotEmpty(result.Todos);
        Assert.All(result.Todos, todo => Assert.True(todo.IsComplete));
        Assert.Contains(result.Evidence, evidence => evidence.Service == "checkout" && evidence.Kind == "status");
        Assert.Contains(result.Evidence, evidence => evidence.Service == "checkout" && evidence.Kind == "runbook");
        Assert.Contains("CHECKOUT-01", result.Answer, StringComparison.OrdinalIgnoreCase);
    }
}
