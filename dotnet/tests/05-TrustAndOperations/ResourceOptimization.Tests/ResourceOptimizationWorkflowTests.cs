using AgenticDesignPatterns.TestSupport;
using System.Net.Http.Json;
using System.Text.Json;
using Chapter16.ResourceOptimization;
using Chapter16.ResourceOptimization.Hosted;
using Chapter16.ResourceOptimization.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.ResourceOptimization.Tests;

public sealed class ResourceOptimizationWorkflowTests
{
    [Theory]
    [InlineData(
        "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen",
        OptimizationCategory.Simple)]
    [InlineData(
        "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty",
        OptimizationCategory.Reasoning)]
    public void Complexity_router_selects_route_at_twenty_word_threshold(
        string request,
        OptimizationCategory expected)
    {
        Assert.Equal(expected, ResourceOptimizationWorkflow.RouteByComplexity(request));
    }

    [Theory]
    [InlineData("{\"classification\":\"simple\"}", OptimizationCategory.Simple)]
    [InlineData("{\"classification\":\"reasoning\"}", OptimizationCategory.Reasoning)]
    [InlineData("{\"classification\":\"internet_search\"}", OptimizationCategory.InternetSearch)]
    public async Task Classifier_accepts_only_known_categories(string output, OptimizationCategory expected)
    {
        using var client = new ScriptedChatClient((output, 2, 1), ("Answer.", 4, 3));
        var result = await ResourceOptimizationWorkflow.RunAsync(
            "A caller request", RoutingStrategy.IntentClassification, client, CreateModelClients(client),
            webSearchAgent: new ChatClientAgent(client, name: "TestWebSearch"));
        Assert.Equal(expected, result.Category);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"classification\":\"other\"}")]
    [InlineData("{\"classification\":0}")]
    public async Task Classifier_rejects_malformed_or_unknown_categories(string output)
    {
        using var client = new ScriptedChatClient((output, 2, 1));
        await Assert.ThrowsAsync<JsonException>(() => ResourceOptimizationWorkflow.RunAsync(
            "A caller request", RoutingStrategy.IntentClassification, client, CreateModelClients(client)));
        Assert.Single(client.Requests);
    }

    [Theory]
    [InlineData("FAST", "fast-deployment")]
    [InlineData("reasoning", "reasoning-deployment")]
    [InlineData("QUALITY", "quality-deployment")]
    public void Deployment_alias_resolution_is_case_insensitive(
        string alias,
        string expectedDeployment)
    {
        var deployments = new Dictionary<string, string>
        {
            ["Fast"] = "fast-deployment",
            ["REASONING"] = "reasoning-deployment",
            ["quality"] = "quality-deployment"
        };

        Assert.Equal(
            expectedDeployment,
            ResourceOptimizationWorkflow.ResolveDeploymentAlias(
                deployments,
                alias,
                "default-deployment"));
    }

    [Fact]
    public void Missing_deployment_alias_uses_the_default()
    {
        var deployments = new Dictionary<string, string>
        {
            ["FAST"] = "fast-deployment"
        };

        Assert.Equal(
            "default-deployment",
            ResourceOptimizationWorkflow.ResolveDeploymentAlias(
                deployments,
                "reasoning",
                "default-deployment"));
    }

    [Fact]
    public async Task Complexity_workflow_uses_selected_route_and_reports_provider_usage()
    {
        using var client = new ScriptedChatClient(
            ("A concise answer.", 5, 4));
        var modelClients = CreateModelClients(client);

        var result = await ResourceOptimizationWorkflow.RunAsync(
            "What is the capital of Australia?",
            RoutingStrategy.Complexity,
            client,
            modelClients);

        Assert.Equal(OptimizationCategory.Simple, result.Category);
        Assert.Equal("fast-deployment", result.ModelDeployment);
        Assert.Equal("A concise answer.", result.Answer);
        Assert.Equal(5, result.InputTokens);
        Assert.Equal(4, result.OutputTokens);
        Assert.Equal(9, result.TotalTokens);
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task Search_route_requires_search_configuration()
    {
        using var client = new ScriptedChatClient(
            ("{\"classification\":\"internet_search\"}", 2, 1));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResourceOptimizationWorkflow.RunAsync(
                "What is the latest Australian Open schedule?",
                RoutingStrategy.IntentClassification,
                client,
                CreateModelClients(client)));

        Assert.Contains("Google Custom Search is not configured", error.ToString());
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task Google_search_client_maps_result_fields_and_limits_query_results()
    {
        var handler = new GoogleSearchHandler();
        using var httpClient = new HttpClient(handler);
        using var searchClient = new GoogleCustomSearchClient("test-key", "test-engine", httpClient);

        var results = await searchClient.SearchAsync("latest schedule");

        Assert.Equal("https://www.googleapis.com/customsearch/v1", handler.RequestUri?.GetLeftPart(UriPartial.Path));
        Assert.Contains("num=3", handler.RequestUri?.Query);
        Assert.Contains("latest%20schedule", handler.RequestUri?.Query);
        Assert.Equal(
            [new GoogleSearchResult("Result title", "Result snippet", "https://example.com/result")],
            results);
    }

    [Fact]
    public async Task Hosted_search_returns_citations_and_combined_provider_usage()
    {
        using var model = new ScriptedChatClient(
            ("""{"classification":"internet_search"}""", 2, 1));
        using var webModel = new ScriptedChatClient(("Current answer.", 6, 5))
        {
            Citation = new CitationAnnotation
            {
                Title = "Official source",
                Url = new Uri("https://example.com/current")
            }
        };
        var webSearchAgent = new ChatClientAgent(webModel, name: "TestWebSearch");
        var agent = new ResourceOptimizationAgent(model, CreateModelClients(model), webSearchAgent);
        await using var host = await HostedResponses.StartAsync(agent);
        using var body = await host.InvokeAsync("What is the latest schedule?", agent.Name);
        var text = HostedResponses.OutputText(body.RootElement);
        Assert.Contains("Classification: InternetSearch", text);
        Assert.Contains("Model deployment: quality-deployment", text);
        Assert.Contains("Current answer.", text);
        Assert.Contains("Official source: https://example.com/current", text);
        Assert.Contains("input=8, output=6, total=14", text);
        Assert.Single(model.Requests);
        Assert.Single(webModel.Requests);
        var usage = body.RootElement.GetProperty("usage");
        Assert.Equal(8, usage.GetProperty("input_tokens").GetInt64());
        Assert.Equal(6, usage.GetProperty("output_tokens").GetInt64());
        Assert.Equal(14, usage.GetProperty("total_tokens").GetInt64());
    }

    [Fact]
    public async Task Hosted_stream_preserves_structured_usage_not_only_display_text()
    {
        using var client = new ScriptedChatClient(
            ("""{"classification":"simple"}""", 2, 1), ("Canberra.", 4, 3));
        var agent = new ResourceOptimizationAgent(
            client, CreateModelClients(client), new ChatClientAgent(client, name: "UnusedSearch"));
        var updates = new List<AgentResponseUpdate>();
        await foreach (var update in agent.RunStreamingAsync("What is the capital of Australia?"))
            updates.Add(update);
        var response = updates.ToAgentResponse();
        Assert.Contains("Canberra.", response.Text);
        Assert.Equal(6, response.Usage?.InputTokenCount);
        Assert.Equal(4, response.Usage?.OutputTokenCount);
        Assert.Equal(10, response.Usage?.TotalTokenCount);
    }

    [Fact]
    public async Task Native_web_search_failure_is_propagated_without_a_google_fallback()
    {
        using var classifier = new ScriptedChatClient(
            ("{\"classification\":\"internet_search\"}", 2, 1));
        using var webModel = new ScriptedChatClient();
        var agent = new ResourceOptimizationAgent(
            classifier,
            CreateModelClients(classifier),
            new ChatClientAgent(webModel, name: "FailingWebSearch"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.RunAsync("What is the latest schedule?"));

        Assert.Contains("A scripted model response is missing", error.ToString());
        Assert.DoesNotContain("Google Custom Search is not configured", error.ToString());
        Assert.Single(classifier.Requests);
        Assert.Single(webModel.Requests);
    }

    private static IReadOnlyDictionary<OptimizationCategory, OptimizationModelClient> CreateModelClients(
        IChatClient client) =>
        new Dictionary<OptimizationCategory, OptimizationModelClient>
        {
            [OptimizationCategory.Simple] = new("fast-deployment", client),
            [OptimizationCategory.Reasoning] = new("reasoning-deployment", client),
            [OptimizationCategory.InternetSearch] = new("quality-deployment", client)
        };

    private sealed class ScriptedChatClient(params (string Text, long Input, long Output)[] responses)
        : IChatClient
    {
        private readonly Queue<(string Text, long Input, long Output)> _responses = new(responses);
        public List<List<ChatMessage>> Requests { get; } = [];
        public CitationAnnotation? Citation { get; init; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(messages.ToList());
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("A scripted model response is missing.");
            }

            var content = new TextContent(response.Text);
            if (Citation is not null)
            {
                content.Annotations = [Citation];
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [content]))
            {
                Usage = new UsageDetails
                {
                    InputTokenCount = response.Input,
                    OutputTokenCount = response.Output,
                    TotalTokenCount = response.Input + response.Output
                }
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The Chapter 16 workflow uses non-streaming model calls.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class GoogleSearchHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    items = new[]
                    {
                        new
                        {
                            title = "Result title",
                            snippet = "Result snippet",
                            link = "https://example.com/result"
                        }
                    }
                })
            };
            return Task.FromResult(response);
        }
    }
}
