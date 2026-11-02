using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;
using Chapter09.LearningAdaptation;
using OpenAI;
using Xunit;

#pragma warning disable OPENAI001

namespace AgenticDesignPatterns.LearningAdaptation.Tests;

public sealed class FoundryEvaluationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Caller_cancellation_interrupts_creation_submission_and_result_requests(int blockedRequest)
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new EvaluationHandler(blockedRequest: blockedRequest);
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var run = FoundryEvaluationDemo.RunAsync(
            client, "model", "baseline", "proposal", cancellationToken: cancellation.Token);
        await handler.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => run.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(handler.RequestCancelled);
        Assert.Equal(blockedRequest, handler.RequestCount);
    }

    [Fact]
    public async Task Polling_deadline_cancels_an_in_flight_status_request()
    {
        using var handler = new EvaluationHandler(blockedRequest: 3, pendingRun: true);
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<TimeoutException>(() => FoundryEvaluationDemo.RunAsync(
            CreateClient(http), "model", "baseline", "proposal",
            pollingTimeout: TimeSpan.FromMilliseconds(100),
            pollingInterval: TimeSpan.Zero));

        Assert.True(handler.RequestCancelled);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task Polling_deadline_interrupts_the_delay_before_another_request()
    {
        using var handler = new EvaluationHandler(pendingRun: true);
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<TimeoutException>(() => FoundryEvaluationDemo.RunAsync(
            CreateClient(http), "model", "baseline", "proposal",
            pollingTimeout: TimeSpan.FromMilliseconds(100),
            pollingInterval: TimeSpan.FromSeconds(30)));

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Caller_cancellation_during_polling_remains_cancellation_not_timeout()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new EvaluationHandler(blockedRequest: 3, pendingRun: true);
        using var http = new HttpClient(handler);
        var run = FoundryEvaluationDemo.RunAsync(
            CreateClient(http), "model", "baseline", "proposal",
            cancellationToken: cancellation.Token,
            pollingInterval: TimeSpan.Zero);
        await handler.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => run.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(handler.RequestCancelled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Incomplete_or_excess_results_fail_instead_of_reporting_a_normal_score(int resultCount)
    {
        using var handler = new EvaluationHandler(resultCount: resultCount);
        using var http = new HttpClient(handler);
        var output = new List<string>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FoundryEvaluationDemo.RunAsync(CreateClient(http), "model", "baseline", "proposal", output.Add));

        Assert.Contains("expected 3 benchmark results", error.Message);
        Assert.DoesNotContain(output, line => line.StartsWith("Baseline task-adherence score:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Complete_paginated_results_report_the_expected_denominator()
    {
        using var handler = new EvaluationHandler(paginated: true);
        using var http = new HttpClient(handler);
        var output = new List<string>();

        await FoundryEvaluationDemo.RunAsync(CreateClient(http), "model", "baseline", "proposal", output.Add);

        Assert.Contains(output, line => line.StartsWith("Baseline task-adherence score: 3/3", StringComparison.Ordinal));
        Assert.Contains(output, line => line.StartsWith("Proposal task-adherence score: 3/3", StringComparison.Ordinal));
        Assert.Equal(7, handler.RequestCount);
    }

    private static OpenAI.Evals.EvaluationClient CreateClient(HttpClient http) =>
        new OpenAIClient(new ApiKeyCredential("test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri("https://evaluation.test/"),
            Transport = new HttpClientPipelineTransport(http)
        }).GetEvaluationClient();

    private sealed class EvaluationHandler(
        int blockedRequest = 0,
        bool pendingRun = false,
        int resultCount = 3,
        bool paginated = false) : HttpMessageHandler
    {
        public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }
        public bool RequestCancelled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == blockedRequest)
            {
                Blocked.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    RequestCancelled = true;
                    throw;
                }
            }

            object response;
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/output_items"))
            {
                var secondPage = request.RequestUri.Query.Contains("after=", StringComparison.Ordinal);
                var count = paginated ? secondPage ? 2 : 1 : resultCount;
                response = new
                {
                    data = Enumerable.Range(0, count).Select(index => new
                    {
                        id = $"item-{index}",
                        results = new[] { new { name = "task_adherence", passed = true } }
                    }).ToArray(),
                    has_more = paginated && !secondPage,
                    last_id = "page-one-last-item"
                };
            }
            else if (RequestCount == 1)
            {
                response = new { id = "eval-test" };
            }
            else
            {
                response = new { id = $"run-{RequestCount}", status = pendingRun ? "queued" : "completed" };
            }

            return new HttpResponseMessage(
                request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response), System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}

#pragma warning restore OPENAI001
