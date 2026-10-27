using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter19.Evaluation;
using Chapter19.Evaluation.Demos;
using Chapter19.Evaluation.Hosted;
using Chapter19.Evaluation.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Xunit;

namespace Evaluation.Tests;

public sealed class EvaluationTests
{
    [Fact]
    public void Exact_match_preserves_notebook_normalization_and_paraphrase_failure()
    {
        Assert.Equal(0, BasicMetricsDemo.ExactMatch(EvaluationSamples.CapitalResponse, EvaluationSamples.CapitalReference));
        Assert.Equal(1, BasicMetricsDemo.ExactMatch("  PARIS  ", "paris"));
        Assert.Equal(0, BasicMetricsDemo.ExactMatch("Paris.", "Paris"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Judgment_rejects_out_of_range_scores(int score) =>
        Assert.Throws<InvalidOperationException>(() => Judgment(score).Validate());

    [Fact]
    public void Judgment_rejects_missing_feedback_and_allows_no_concerns()
    {
        Judgment(4).Validate();
        Assert.Throws<InvalidOperationException>(() => (Judgment(4) with { DetailedFeedback = ["incomplete"] }).Validate());
        Assert.Throws<InvalidOperationException>(() => (Judgment(4) with { DetailedFeedback = Enumerable.Repeat("Clarity only", 5).ToArray() }).Validate());
        Assert.Throws<InvalidOperationException>(() => (Judgment(4) with { Rationale = "" }).Validate());
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task Meai_adapter_preserves_score_and_threshold(int score, bool failed)
    {
        using var model = new JudgeClient(score);
        IEvaluator evaluator = new LegalSurveyEvaluator();
        var result = await evaluator.EvaluateAsync(
            [], new ChatResponse(new ChatMessage(ChatRole.Assistant, "Caller survey question")),
            new ChatConfiguration(model));
        var metric = result.Get<NumericMetric>(LegalSurveyEvaluator.MetricName);
        Assert.Equal(score, metric.Value);
        Assert.Equal(failed, metric.Interpretation?.Failed);
        Assert.Equal("Caller survey question", Assert.Single(model.Questions));
    }

    [Fact]
    public async Task Judge_rejects_empty_input_and_propagates_cancellation_and_invalid_output()
    {
        using var model = new JudgeClient(6);
        await Assert.ThrowsAsync<ArgumentException>(() => LegalSurveyJudge.EvaluateAsync(model, " "));
        Assert.Empty(model.Questions);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LegalSurveyJudge.EvaluateAsync(model, "question", cancellation.Token));
        Assert.Empty(model.Questions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => LegalSurveyJudge.EvaluateAsync(model, "question"));
    }

    [Fact]
    public async Task Hosted_requests_use_only_latest_question_without_session_leakage()
    {
        using var model = new JudgeClient(4);
        var agent = new SurveyEvaluationAgent(model);
        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, "Earlier question"), new ChatMessage(ChatRole.User, "Current question")],
            session);
        await agent.RunAsync("Unrelated caller question", session);
        Assert.Equal(["Current question", "Unrelated caller question"], model.Questions);
        Assert.Equal(4, JsonSerializer.Deserialize<SurveyJudgment>(response.Text)!.OverallScore);
    }

    [Fact]
    public async Task Hosted_stream_buffers_one_valid_judgment()
    {
        using var model = new JudgeClient(4);
        var updates = new List<AgentResponseUpdate>();
        await foreach (var update in new SurveyEvaluationAgent(model).RunStreamingAsync("Question"))
            updates.Add(update);
        Assert.Equal(4, JsonSerializer.Deserialize<SurveyJudgment>(Assert.Single(updates).Text)!.OverallScore);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Responses_endpoint_returns_the_actual_judgment(bool streaming)
    {
        using var model = new JudgeClient(4);
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.Services.AddFoundryResponses(new SurveyEvaluationAgent(model));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();
        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                input = "Caller-owned survey question", stream = streaming, store = false
            });
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            if (streaming)
            {
                Assert.Contains("response.completed", body);
                Assert.Contains("overall_score", body);
            }
            else
            {
                using var document = JsonDocument.Parse(body);
                Assert.Equal("completed", document.RootElement.GetProperty("status").GetString());
                var text = string.Concat(document.RootElement.GetProperty("output").EnumerateArray()
                    .SelectMany(item => item.GetProperty("content").EnumerateArray())
                    .Where(content => content.GetProperty("type").GetString() == "output_text")
                    .Select(content => content.GetProperty("text").GetString()));
                Assert.Equal(4, JsonSerializer.Deserialize<SurveyJudgment>(text)!.OverallScore);
            }
            Assert.Equal("Caller-owned survey question", Assert.Single(model.Questions));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Theory]
    [InlineData(0, "completed", 4)]
    [InlineData(1, "error", 4)]
    [InlineData(1, "completed", 0)]
    [InlineData(1, "completed", 6)]
    public void Foundry_output_rejects_missing_failed_or_invalid_measurements(int count, string status, int score)
    {
        var item = new { status, results = new[] { new { name = "similarity", score, reason = "Comparison" } } };
        var output = JsonSerializer.SerializeToElement(new { has_more = false, data = Enumerable.Repeat(item, count).ToArray() });
        Assert.Throws<InvalidOperationException>(() => FoundryEvaluationDemo.ValidateOutput(output));
    }

    [Fact]
    public void Foundry_output_accepts_a_measured_low_score()
    {
        var output = JsonSerializer.SerializeToElement(new
        {
            has_more = false,
            data = new[] { new { status = "completed", results = new[] { new { name = "similarity", score = 2, reason = "Different meaning", label = "fail", passed = false } } } }
        });
        FoundryEvaluationDemo.ValidateOutput(output);
    }

    private static SurveyJudgment Judgment(int score) => new(score, "Review rationale",
        ["Clarity: clarify terms", "Neutrality: remove leading words", "Relevance: focus topic",
         "Completeness: add context", "Audience: define audience"], [], "Revise");

    private sealed class JudgeClient(int score) : IChatClient
    {
        public List<string> Questions { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Questions.Add(messages.Last(message => message.Role == ChatRole.User).Text);
            Assert.Contains("Clarity & Precision", options?.Instructions ??
                string.Join('\n', messages.Where(message => message.Role == ChatRole.System).Select(message => message.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(Judgment(score)))));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
