using System.ClientModel;
using System.Text.Json;
using AgenticDesignPatterns.Shared;
using Azure.AI.Projects;
using Azure.Identity;
using Chapter09.LearningAdaptation.Models;
using OpenAI.Evals;

#pragma warning disable OPENAI001

namespace Chapter09.LearningAdaptation;

public static class FoundryEvaluationDemo
{
    public static async Task RunAsync(
        FoundrySettings settings,
        string deployment,
        string baselineInstructions,
        string proposedInstructions,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineInstructions);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedInstructions);
        cancellationToken.ThrowIfCancellationRequested();

        var projectClient = new AIProjectClient(
            new Uri(settings.ProjectEndpoint),
            tokenProvider: new DefaultAzureCredential());
        EvaluationClient evaluationClient = projectClient.ProjectOpenAIClient.GetEvaluationClient();
        await RunAsync(
            evaluationClient, deployment, baselineInstructions, proposedInstructions,
            writeOutput, cancellationToken);
    }

    internal static async Task RunAsync(
        EvaluationClient evaluationClient,
        string deployment,
        string baselineInstructions,
        string proposedInstructions,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default,
        TimeSpan? pollingTimeout = null,
        TimeSpan? pollingInterval = null)
    {
        var evaluationId = await CreateEvaluationAsync(
            evaluationClient,
            deployment,
            cancellationToken);

        writeOutput?.Invoke("Foundry evaluator: builtin.task_adherence");
        var baselineRunId = await RunEvaluationAsync(
            evaluationClient,
            evaluationId,
            deployment,
            baselineInstructions,
            "baseline",
            writeOutput,
            cancellationToken,
            pollingTimeout,
            pollingInterval);
        var proposalRunId = await RunEvaluationAsync(
            evaluationClient,
            evaluationId,
            deployment,
            proposedInstructions,
            "proposal",
            writeOutput,
            cancellationToken,
            pollingTimeout,
            pollingInterval);
        var baselineScore = await GetTaskAdherenceScoreAsync(
            evaluationClient,
            evaluationId,
            baselineRunId,
            cancellationToken);
        var proposalScore = await GetTaskAdherenceScoreAsync(
            evaluationClient,
            evaluationId,
            proposalRunId,
            cancellationToken);

        writeOutput?.Invoke($"Foundry evaluation: {evaluationId}");
        writeOutput?.Invoke(
            $"Baseline task-adherence score: {baselineScore.Passed}/{baselineScore.Total} cases passed (run {baselineRunId}).");
        writeOutput?.Invoke(
            $"Proposal task-adherence score: {proposalScore.Passed}/{proposalScore.Total} cases passed (run {proposalRunId}).");
    }

    private static async Task<string> CreateEvaluationAsync(
        EvaluationClient evaluationClient,
        string deployment,
        CancellationToken cancellationToken)
    {
        object dataSourceConfig = new
        {
            type = "custom",
            item_schema = new
            {
                type = "object",
                properties = new
                {
                    instructions = new { type = "string" },
                    query = new { type = "string" }
                },
                required = new[] { "instructions", "query" }
            },
            include_sample_schema = true
        };
        object[] testingCriteria =
        [
            new
            {
                type = "azure_ai_evaluator",
                name = "task_adherence",
                evaluator_name = "builtin.task_adherence",
                initialization_parameters = new { deployment_name = deployment },
                data_mapping = new
                {
                    query = "{{item.query}}",
                    response = "{{sample.output_text}}"
                }
            }
        ];

        var evaluationData = BinaryData.FromObjectAsJson(new
        {
            name = $"chapter09-prompt-evaluation-{Guid.NewGuid():N}",
            data_source_config = dataSourceConfig,
            testing_criteria = testingCriteria
        });
        using var evaluationContent = BinaryContent.Create(evaluationData);
        var evaluation = await evaluationClient.CreateEvaluationAsync(
            evaluationContent,
            options: new() { CancellationToken = cancellationToken });
        cancellationToken.ThrowIfCancellationRequested();
        return GetString(evaluation, "id");
    }

    private static async Task<string> RunEvaluationAsync(
        EvaluationClient evaluationClient,
        string evaluationId,
        string deployment,
        string instructions,
        string label,
        Action<string>? writeOutput,
        CancellationToken cancellationToken,
        TimeSpan? pollingTimeout,
        TimeSpan? pollingInterval)
    {
        var dataSource = new
        {
            type = "azure_ai_target_completions",
            source = new
            {
                type = "file_content",
                content = EvaluationBenchmark.Cases.Select(benchmarkCase => new
                {
                    item = new { instructions, query = benchmarkCase.Input }
                }).ToArray()
            },
            input_messages = new
            {
                type = "template",
                template = new object[]
                {
                    new
                    {
                        type = "message",
                        role = "developer",
                        content = new { type = "input_text", text = "{{item.instructions}}" }
                    },
                    new
                    {
                        type = "message",
                        role = "user",
                        content = new { type = "input_text", text = "{{item.query}}" }
                    }
                }
            },
            target = new
            {
                type = "azure_ai_model",
                model = deployment,
                sampling_params = new { top_p = 1.0f, max_completion_tokens = 512 }
            }
        };
        var runData = BinaryData.FromObjectAsJson(new
        {
            name = $"chapter09-{label}-{Guid.NewGuid():N}",
            data_source = dataSource
        });
        using var runContent = BinaryContent.Create(runData);
        var evaluationRun = await evaluationClient.CreateEvaluationRunAsync(
            evaluationId: evaluationId,
            content: runContent,
            options: new() { CancellationToken = cancellationToken });
        cancellationToken.ThrowIfCancellationRequested();
        var runId = GetString(evaluationRun, "id");
        writeOutput?.Invoke($"Submitted {label} evaluation run: {runId}");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(pollingTimeout ?? TimeSpan.FromMinutes(5));
        try
        {
            var status = GetString(evaluationRun, "status");
            while (status is not ("completed" or "failed" or "canceled"))
            {
                await Task.Delay(pollingInterval ?? TimeSpan.FromSeconds(2), deadline.Token);
                evaluationRun = await evaluationClient.GetEvaluationRunAsync(
                    evaluationId: evaluationId,
                    evaluationRunId: runId,
                    options: new() { CancellationToken = deadline.Token });
                deadline.Token.ThrowIfCancellationRequested();
                status = GetString(evaluationRun, "status");
            }

            deadline.Token.ThrowIfCancellationRequested();
            if (status != "completed")
            {
                throw new InvalidOperationException(
                    $"Foundry {label} evaluation ended with status '{status}': {evaluationRun.GetRawResponse().Content}");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"Foundry {label} evaluation did not complete within its polling deadline.");
        }

        return runId;
    }

    private static async Task<EvaluationScore> GetTaskAdherenceScoreAsync(
        EvaluationClient evaluationClient,
        string evaluationId,
        string runId,
        CancellationToken cancellationToken)
    {
        var passed = 0;
        var total = 0;
        var hasMore = false;
        string? after = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outputPage = await evaluationClient.GetEvaluationRunOutputItemsAsync(
                evaluationId: evaluationId,
                evaluationRunId: runId,
                limit: null,
                order: "asc",
                after: after,
                outputItemStatus: null,
                options: new() { CancellationToken = cancellationToken });
            cancellationToken.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(outputPage.GetRawResponse().Content.ToMemory());
            var root = document.RootElement;
            foreach (var outputItem in root.GetProperty("data").EnumerateArray())
            {
                if (!outputItem.TryGetProperty("results", out var results) ||
                    results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var result in results.EnumerateArray())
                {
                    var evaluatorName = result.TryGetProperty("name", out var name)
                        ? name.GetString()
                        : result.TryGetProperty("metric", out var metric)
                            ? metric.GetString()
                            : null;
                    if (!string.Equals(evaluatorName, "task_adherence", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (result.TryGetProperty("passed", out var passedValue) &&
                        passedValue.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        total++;
                        if (passedValue.GetBoolean())
                        {
                            passed++;
                        }
                    }
                    else if (result.TryGetProperty("label", out var label) &&
                             label.ValueKind == JsonValueKind.String &&
                             (string.Equals(label.GetString(), "pass", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(label.GetString(), "fail", StringComparison.OrdinalIgnoreCase)))
                    {
                        total++;
                        if (string.Equals(label.GetString(), "pass", StringComparison.OrdinalIgnoreCase))
                        {
                            passed++;
                        }
                    }
                }
            }

            hasMore = root.GetProperty("has_more").GetBoolean();
            after = hasMore ? root.GetProperty("last_id").GetString() : null;
        }
        while (hasMore);

        if (total != EvaluationBenchmark.Cases.Count)
        {
            throw new InvalidOperationException(
                $"Foundry evaluation run '{runId}' returned {total} usable task_adherence results; " +
                $"expected {EvaluationBenchmark.Cases.Count} benchmark results.");
        }

        return new EvaluationScore(passed, total);
    }

    private static string GetString(ClientResult result, string propertyName)
    {
        using var document = JsonDocument.Parse(result.GetRawResponse().Content.ToMemory());
        return document.RootElement.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"Foundry response did not contain '{propertyName}'.");
    }

    private sealed record EvaluationScore(int Passed, int Total);
}

#pragma warning restore OPENAI001
