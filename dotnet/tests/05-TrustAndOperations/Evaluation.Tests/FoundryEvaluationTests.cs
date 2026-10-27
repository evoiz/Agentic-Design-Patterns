using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;
using Chapter19.Evaluation;
using Chapter19.Evaluation.Demos;
using OpenAI;
using Xunit;

#pragma warning disable OPENAI001

namespace Evaluation.Tests;

public sealed class FoundryEvaluationTests
{
    [Fact]
    public async Task Cloud_request_maps_the_original_paraphrase_and_returns_a_measured_result()
    {
        using var handler = new EvaluationHandler();
        using var http = new HttpClient(handler);
        await FoundryEvaluationDemo.RunAsync(CreateClient(http), "judge-model");
        Assert.Equal(3, handler.Requests);
        Assert.True(handler.CheckedSchema);
        Assert.True(handler.CheckedSample);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Cancellation_reaches_creation_submission_and_result_requests(int blockedRequest)
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new EvaluationHandler(blockedRequest);
        using var http = new HttpClient(handler);
        var run = FoundryEvaluationDemo.RunAsync(CreateClient(http), "judge-model", cancellation.Token);
        await handler.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(blockedRequest, handler.Requests);
    }

    [Fact]
    public async Task A_failed_cloud_run_does_not_report_a_normal_result()
    {
        using var handler = new EvaluationHandler(failedRun: true);
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FoundryEvaluationDemo.RunAsync(CreateClient(http), "judge-model"));
        Assert.Contains("failed", error.Message);
        Assert.Equal(2, handler.Requests);
    }

    private static OpenAI.Evals.EvaluationClient CreateClient(HttpClient http) =>
        new OpenAIClient(new ApiKeyCredential("test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri("https://evaluation.test/"),
            Transport = new HttpClientPipelineTransport(http)
        }).GetEvaluationClient();

    private sealed class EvaluationHandler(int blockedRequest = 0, bool failedRun = false) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public bool CheckedSchema { get; private set; }
        public bool CheckedSample { get; private set; }
        public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Requests == blockedRequest)
            {
                Blocked.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            object payload;
            if (Requests == 1)
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var criteria = body.RootElement.GetProperty("testing_criteria")[0];
                Assert.Equal("builtin.similarity", criteria.GetProperty("evaluator_name").GetString());
                Assert.Equal("judge-model", criteria.GetProperty("initialization_parameters").GetProperty("deployment_name").GetString());
                Assert.Equal("{{item.ground_truth}}", criteria.GetProperty("data_mapping").GetProperty("ground_truth").GetString());
                CheckedSchema = true;
                payload = new { id = "eval-test" };
            }
            else if (Requests == 2)
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var source = body.RootElement.GetProperty("data_source");
                Assert.Equal("jsonl", source.GetProperty("type").GetString());
                var items = source.GetProperty("source").GetProperty("content");
                var sample = items[0].GetProperty("item");
                Assert.Equal(1, items.GetArrayLength());
                Assert.Equal(EvaluationSamples.CapitalQuery, sample.GetProperty("query").GetString());
                Assert.Equal(EvaluationSamples.CapitalResponse, sample.GetProperty("response").GetString());
                Assert.Equal(EvaluationSamples.CapitalReference, sample.GetProperty("ground_truth").GetString());
                CheckedSample = true;
                payload = new { id = "run-test", status = failedRun ? "failed" : "completed" };
            }
            else
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.EndsWith("/evals/eval-test/runs/run-test/output_items", request.RequestUri!.AbsolutePath);
                payload = new
                {
                    has_more = false,
                    data = new[]
                    {
                        new
                        {
                            status = "completed",
                            results = new[] { new { name = "similarity", status = "completed", score = 5, reason = "Same meaning" } }
                        }
                    }
                };
            }
            return new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}

#pragma warning restore OPENAI001
