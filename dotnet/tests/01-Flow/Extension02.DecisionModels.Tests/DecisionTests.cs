using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgenticDesignPatterns.Shared;
using AgenticDesignPatterns.TestSupport;
using Azure.AI.AgentServer.Core;
using Extension02.DecisionModels;
using Extension02.DecisionModels.Hosted;
using ElBruno.AI.Decisions;
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace AgenticDesignPatterns.Extension02.DecisionModels.Tests;

public sealed class DecisionTests
{
    [Fact]
    public void Options_without_a_variant_use_all_and_parse_from_the_first_argument()
    {
        Assert.Equal(new DemoArguments("all", "microsoft", null), DemoArguments.Parse([]));
        Assert.Equal(new DemoArguments("all", "microsoft", "My account is locked"),
            DemoArguments.Parse(["--input", "My account is locked"]));
        Assert.Equal(new DemoArguments("all", "openai", "request"),
            DemoArguments.Parse(["--provider", "openai", "--input", "request"]));
        Assert.Equal(new DemoArguments("maf", "openai", "request"),
            DemoArguments.Parse(["maf", "--input", "request", "--provider", "openai"]));
    }

    [Theory]
    [InlineData("--input")]
    [InlineData("--provider")]
    [InlineData("unknown")]
    public void Invalid_or_incomplete_console_arguments_are_rejected(string argument) =>
        Assert.Throws<ArgumentException>(() => DemoArguments.Parse([argument]));

    [Fact]
    public void Unsupported_options_and_providers_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => DemoArguments.Parse(["--unknown", "value"]));
        Assert.Throws<ArgumentException>(() => DemoArguments.Parse(["--provider", "unknown"]));
        Assert.ThrowsAny<ArgumentException>(() => DemoArguments.Parse(["--input", " "]));
    }

    [Theory]
    [InlineData(0.799, 0.1, 0.0, "human-review")]
    [InlineData(0.8, 0.1, 0.0, "draft-response")]
    [InlineData(0.9, 0.1, 0.499, "draft-response")]
    [InlineData(0.9, 0.99, 0.5, "refund-review")]
    public void Policy_uses_selected_probability_not_provider_confidence(
        double probability, double confidence, double refund, string expected)
    {
        var result = DecisionWorkflow.ApplyPolicy("request",
            Team(probability, confidence), new ScoreDecision([0, 1, 0, 0]),
            new AssessmentDecision(refund));
        Assert.Equal(expected, result.Disposition);
    }

    [Theory]
    [InlineData(0.51, "normal")]
    [InlineData(0.5, "urgent")]
    [InlineData(0.49, "urgent")]
    public void Priority_uses_fractional_score_at_explicit_boundary(double minor, string expected)
    {
        var result = DecisionWorkflow.ApplyPolicy("request", Team(),
            new ScoreDecision([0, minor, 1 - minor, 0]), new AssessmentDecision(0));
        Assert.Equal(expected, result.Priority);
    }

    [Fact]
    public void Malformed_distribution_cannot_become_a_route()
    {
        Assert.Throws<InvalidOperationException>(() => DecisionWorkflow.ApplyPolicy("request",
            new ChoiceDecision("technical", new Dictionary<string, double> { ["technical"] = 1 }),
            new ScoreDecision([0, 1, 0, 0]), new AssessmentDecision(0)));
        Assert.Throws<InvalidOperationException>(() => DecisionWorkflow.ApplyPolicy("request",
            Team(), new ScoreDecision([0, 0, 0, 0]), new AssessmentDecision(0)));
    }

    [Theory]
    [InlineData(FoundryDecisionApi.SystemOne, "/providers/microsoft/v1/systemone")]
    [InlineData(FoundryDecisionApi.OpenAIDecisions, "/openai/v1/decisions")]
    public async Task Real_library_sends_distinct_contracts_and_preserves_usage(
        FoundryDecisionApi api, string path)
    {
        using var handler = new DecisionHandler(api);
        using var http = new HttpClient(handler);
        using var client = Client(http, api);
        var result = await DecisionWorkflow.EvaluateAsync(client, "My account is locked.");
        Assert.Equal("account", result.Team.Choice);
        Assert.Equal(0.93, result.Team.Probabilities["account"]);
        Assert.Equal(0.7, result.Team.Confidence);
        Assert.Equal(1.5, result.Severity.Score);
        Assert.Equal(0.1, result.Refund.Probability);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(path, request.Path);
            Assert.Equal("fixture-deployment", request.Body["model"]!.GetValue<string>());
            if (api == FoundryDecisionApi.SystemOne)
            {
                Assert.Equal("My account is locked.", request.Body["state"]!.GetValue<string>());
                Assert.IsType<JsonObject>(request.Body["questions"]);
            }
            else
            {
                Assert.Equal("My account is locked.", request.Body["input"]!.GetValue<string>());
                Assert.IsType<JsonArray>(request.Body["questions"]);
            }
        });
        Assert.Equal(0, result.Team.Usage!.OutputTokens);
        Assert.Equal(12, result.Team.Usage.TotalTokens);
    }

    [Fact]
    public async Task Refusal_and_http_error_propagate_without_approval()
    {
        using var handler = new DecisionHandler(FoundryDecisionApi.OpenAIDecisions)
        {
            ResponseOverride = """{"answers":[{"name":"decision","type":"refusal"}]}"""
        };
        using var http = new HttpClient(handler);
        using var client = Client(http, FoundryDecisionApi.OpenAIDecisions);
        await Assert.ThrowsAsync<DecisionException>(() =>
            DecisionWorkflow.EvaluateAsync(client, "request"));
        handler.Status = HttpStatusCode.TooManyRequests;
        var error = await Assert.ThrowsAsync<DecisionException>(() =>
            DecisionWorkflow.EvaluateAsync(client, "request"));
        Assert.Equal(429, error.StatusCode);
    }

    [Theory]
    [InlineData(0.99, 0.0, "draft-response", 1)]
    [InlineData(0.79, 0.0, "human-review", 0)]
    [InlineData(0.99, 0.9, "refund-review", 0)]
    public async Task MAF_conditional_edges_only_generate_an_admitted_draft(
        double probability, double refund, string disposition, int chatCalls)
    {
        var decisions = new ScriptedDecisions(probability, refund);
        using var chat = new ScriptedChat();
        var result = await DecisionAgentWorkflow.RunAsync(decisions, chat, "Original caller request");
        Assert.Equal(disposition, result.Decision.Disposition);
        Assert.Equal(chatCalls, chat.Inputs.Count);
        if (chatCalls != 0)
        {
            Assert.Equal("Original caller request", chat.Inputs.Single());
            Assert.Equal("Specialist draft.", result.Response);
        }
    }

    [Fact]
    public async Task Unclear_route_always_requires_review()
    {
        var client = new ScriptedDecisions(1, 0, unclear: true);
        using var chat = new ScriptedChat();
        var result = await DecisionAgentWorkflow.RunAsync(client, chat, "Ambiguous request");
        Assert.Equal("human-review", result.Decision.Disposition);
        Assert.Empty(chat.Inputs);
    }

    [Fact]
    public async Task Hosted_requests_are_isolated_and_stream_only_completed_results()
    {
        var decisions = new ScriptedDecisions();
        using var chat = new ScriptedChat();
        var agent = new DecisionSupportAgent(decisions, chat);
        var session = await agent.CreateSessionAsync();
        await agent.RunAsync("first", session);
        var updates = new List<string>();
        await foreach (var update in agent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "old"), new ChatMessage(ChatRole.User, "second")], session))
        {
            updates.Add(update.Text);
        }
        Assert.Single(updates);
        using var result = JsonDocument.Parse(updates[0]);
        Assert.Equal("second", result.RootElement.GetProperty("Decision").GetProperty("Input").GetString());
        Assert.Equal(["first", "second"], chat.Inputs);
    }

    [Fact]
    public async Task Cancellation_and_model_failure_do_not_return_success()
    {
        using var chat = new ScriptedChat();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DecisionAgentWorkflow.RunAsync(new ScriptedDecisions(), chat, "request",
                cancellationToken: cancellation.Token));
        var failing = new ScriptedDecisions { Fail = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DecisionAgentWorkflow.RunAsync(failing, chat, "request"));
        Assert.Empty(chat.Inputs);
    }

    [Fact]
    public void Hosted_configuration_never_uses_shared_api_key()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Foundry:ProjectEndpoint"] = "https://example.services.ai.azure.com/api/projects/example",
                ["Foundry:ApiKey"] = "synthetic-key",
                ["Foundry:ModelDeployments:decision"] = "fixture-deployment"
            }).Build();
        Assert.Empty(DecisionConfiguration.LoadSettings(configuration, hosted: true).ApiKey);
        var settings = DecisionConfiguration.LoadSettings(configuration);
        Assert.Equal("synthetic-key", settings.ApiKey);
        Assert.Throws<InvalidOperationException>(() => DecisionConfiguration.CreateClient(settings, "openai"));
        Assert.Throws<ArgumentException>(() => DecisionConfiguration.CreateClient(settings, "unknown"));
    }

    [Theory]
    [InlineData(0.99, 0.0, "draft-response")]
    [InlineData(0.79, 0.0, "human-review")]
    [InlineData(0.99, 0.9, "refund-review")]
    public async Task Responses_protocol_returns_actual_typed_workflow_output(
        double probability, double refund, string disposition)
    {
        using var chat = new ScriptedChat();
        await using var host = await HostedResponses.StartAsync(new DecisionSupportAgent(
            new ScriptedDecisions(probability, refund), chat));
        using var document = await host.InvokeAsync("Caller support request", "decision-models");
        Assert.Equal("completed", document.RootElement.GetProperty("status").GetString());
        using var result = JsonDocument.Parse(HostedResponses.OutputText(document.RootElement));
        Assert.Equal(disposition, result.RootElement.GetProperty("Decision").GetProperty("Disposition").GetString());
        Assert.Equal("Caller support request", result.RootElement.GetProperty("Decision").GetProperty("Input").GetString());
    }

    private static ChoiceDecision Team(double probability = 0.99, double confidence = 0.7, bool unclear = false) =>
        new(unclear ? "unclear" : "account", new Dictionary<string, double>
        {
            ["billing"] = 0, ["technical"] = 0,
            ["account"] = unclear ? 1 - probability : probability,
            ["unclear"] = unclear ? probability : 1 - probability
        }, confidence);

    private static FoundryDecisionClient Client(HttpClient http, FoundryDecisionApi api) => new(http,
        new FoundryDecisionOptions
        {
            Endpoint = new Uri("https://example.services.ai.azure.com"),
            ApiKey = "synthetic-key", Api = api, Model = "fixture-deployment"
        });

    private sealed class ScriptedDecisions(double probability = 0.99, double refund = 0, bool unclear = false)
        : IDecisionClient
    {
        public bool Fail { get; init; }
        public Task<ChoiceDecision> ChooseAsync(string input, string instructions,
            IReadOnlyDictionary<string, string?> options, CancellationToken cancellationToken = default) =>
            Fail ? throw new DecisionException("Synthetic failure.") :
                Task.FromResult(Team(probability, unclear: unclear));
        public Task<ScoreDecision> ScoreAsync(string input, string instructions,
            IReadOnlyList<string> rubric, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScoreDecision([0, 0.5, 0.5, 0]));
        public Task<AssessmentDecision> AssessAsync(string input, string proposition,
            CancellationToken cancellationToken = default) => Task.FromResult(new AssessmentDecision(refund));
    }

    private sealed class ScriptedChat : IChatClient
    {
        public List<string> Inputs { get; } = [];
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Inputs.Add(messages.Last(message => message.Role == ChatRole.User).Text!);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Specialist draft.")));
        }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The workflow must use non-streaming chat.");
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class DecisionHandler(FoundryDecisionApi api) : HttpMessageHandler
    {
        public List<(string Path, JsonObject Body)> Requests { get; } = [];
        public string? ResponseOverride { get; set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            Requests.Add((request.RequestUri!.AbsolutePath, body));
            var question = api == FoundryDecisionApi.SystemOne
                ? body["questions"]!["decision"]! : body["questions"]![0]!;
            var type = question["type"]!.GetValue<string>();
            var answer = new JsonObject { ["type"] = type, ["name"] = "decision" };
            if (type == "choice")
            {
                answer["choice"] = "account";
                answer["confidence"] = 0.7;
                answer["probabilities"] = api == FoundryDecisionApi.SystemOne
                    ? JsonSerializer.SerializeToNode(new Dictionary<string, double>
                    {
                        ["account"] = 0.93, ["billing"] = 0.02, ["technical"] = 0.03, ["unclear"] = 0.02
                    })
                    : JsonSerializer.SerializeToNode(new[]
                    {
                        new { value = "account", probability = 0.93 }, new { value = "billing", probability = 0.02 },
                        new { value = "technical", probability = 0.03 }, new { value = "unclear", probability = 0.02 }
                    });
            }
            else if (type == "score")
            {
                answer["score"] = 1.5;
                answer["confidence"] = 0.5;
                answer["probabilities"] = api == FoundryDecisionApi.SystemOne
                    ? JsonSerializer.SerializeToNode(new Dictionary<string, double>
                        { ["0"] = 0, ["1"] = 0.5, ["2"] = 0.5, ["3"] = 0 })
                    : JsonSerializer.SerializeToNode(new[]
                    {
                        new { value = 0, label = "0", probability = 0.0 },
                        new { value = 1, label = "1", probability = 0.5 },
                        new { value = 2, label = "2", probability = 0.5 },
                        new { value = 3, label = "3", probability = 0.0 }
                    });
            }
            else
            {
                answer[type == "noul" ? "noul" : "probability"] = 0.1;
            }
            var response = new JsonObject
            {
                ["answers"] = api == FoundryDecisionApi.SystemOne
                    ? new JsonObject { ["decision"] = answer } : new JsonArray(answer),
                ["usage"] = new JsonObject { ["input_tokens"] = 12, ["output_tokens"] = 0, ["total_tokens"] = 12 }
            };
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(ResponseOverride ?? response.ToJsonString(), Encoding.UTF8, "application/json")
            };
        }
    }
}
