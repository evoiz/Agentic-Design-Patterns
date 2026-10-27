using Azure.AI.AgentServer.Responses;
using Azure.AI.AgentServer.Responses.Models;
using Chapter07.MultiAgent.Distributed;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgenticDesignPatterns.MultiAgent.Tests;

[CollectionDefinition("Hosted environment", DisableParallelization = true)]
public sealed class HostedEnvironmentCollection;

[Collection("Hosted environment")]
public sealed class HostedRequestIsolationTests
{
#pragma warning disable MAAI001
    [Fact]
    public async Task Hosted_request_without_platform_user_header_gets_a_fresh_partition()
    {
        var previous = Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT");
        Environment.SetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT", "true");
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<HostedSessionIsolationKeyProvider,
                Chapter07.MultiAgent.Hosted.RequestIsolationKeyProvider>();
            services.AddFoundryResponses(new StatelessRequestAgent("test",
                (input, _) => Task.FromResult(input)));
            using var container = services.BuildServiceProvider();
            var provider = container.GetRequiredService<HostedSessionIsolationKeyProvider>();
            var firstRequest = new TestResponseContext("response-1");
            Assert.True(string.IsNullOrWhiteSpace(firstRequest.PlatformContext.UserIdKey));

            var first = await provider.GetKeysAsync(firstRequest, new CreateResponse(), CancellationToken.None);
            var second = await provider.GetKeysAsync(
                new TestResponseContext("response-2"), new CreateResponse(), CancellationToken.None);

            Assert.Equal("request:response-1", first!.UserId);
            Assert.Equal("request:response-2", second!.UserId);
            Assert.NotEqual(first.UserId, second.UserId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT", previous);
        }
    }
#pragma warning restore MAAI001

    private sealed class TestResponseContext(string responseId) : ResponseContext(responseId);
}
