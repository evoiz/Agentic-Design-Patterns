using System.ClientModel;
using System.Text.Json;
using AgenticDesignPatterns.Shared;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Evals;

#pragma warning disable OPENAI001

namespace Chapter19.Evaluation.Demos;

public static class FoundryEvaluationDemo
{
    public static async Task RunAsync(
        FoundrySettings settings,
        string deployment,
        CancellationToken cancellationToken = default)
    {
        var project = new AIProjectClient(new Uri(settings.ProjectEndpoint), new DefaultAzureCredential());
        await RunAsync(project.ProjectOpenAIClient.GetEvaluationClient(), deployment, cancellationToken);
    }

    public static async Task RunAsync(
        EvaluationClient client,
        string deployment,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n--- Foundry-native similarity: notebook paraphrase ---");
        using var evaluationContent = BinaryContent.Create(BinaryData.FromObjectAsJson(new
        {
            name = $"chapter19-response-evaluation-{Guid.NewGuid():N}",
            data_source_config = new
            {
                type = "custom",
                item_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string" },
                        response = new { type = "string" },
                        ground_truth = new { type = "string" }
                    },
                    required = new[] { "query", "response", "ground_truth" }
                }
            },
            testing_criteria = new[]
            {
                new
                {
                    type = "azure_ai_evaluator",
                    name = "similarity",
                    evaluator_name = "builtin.similarity",
                    initialization_parameters = new { deployment_name = deployment },
                    data_mapping = new
                    {
                        query = "{{item.query}}",
                        response = "{{item.response}}",
                        ground_truth = "{{item.ground_truth}}"
                    }
                }
            }
        }));
        var evaluation = await client.CreateEvaluationAsync(
            evaluationContent, options: new() { CancellationToken = cancellationToken });
        var evaluationId = ReadString(evaluation, "id");
        Console.WriteLine($"Evaluation: {evaluationId}");
        using var runContent = BinaryContent.Create(BinaryData.FromObjectAsJson(new
        {
            name = "chapter19-notebook-paraphrase",
            data_source = new
            {
                type = "jsonl",
                source = new
                {
                    type = "file_content",
                    content = new[]
                    {
                        new
                        {
                            item = new
                            {
                                query = EvaluationSamples.CapitalQuery,
                                response = EvaluationSamples.CapitalResponse,
                                ground_truth = EvaluationSamples.CapitalReference
                            }
                        }
                    }
                }
            }
        }));
        var run = await client.CreateEvaluationRunAsync(
            evaluationId, runContent, options: new() { CancellationToken = cancellationToken });
        var runId = ReadString(run, "id");
        Console.WriteLine($"Run: {runId}");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            while (ReadString(run, "status") is not ("completed" or "failed" or "canceled" or "cancelled"))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), deadline.Token);
                run = await client.GetEvaluationRunAsync(
                    evaluationId, runId, options: new() { CancellationToken = deadline.Token });
            }
            if (ReadString(run, "status") != "completed")
            {
                throw new InvalidOperationException($"Foundry evaluation '{runId}' ended with status '{ReadString(run, "status")}'.");
            }
            var items = await client.GetEvaluationRunOutputItemsAsync(
                evaluationId, runId, limit: 10, order: "asc", after: null, outputItemStatus: null,
                options: new() { CancellationToken = deadline.Token });
            using var document = JsonDocument.Parse(items.GetRawResponse().Content.ToMemory());
            ValidateOutput(document.RootElement);
            var result = document.RootElement.GetProperty("data")[0].GetProperty("results")[0];
            Console.WriteLine($"Foundry similarity: {result.GetProperty("score").GetDouble()}/5");
            Console.WriteLine($"Reason: {result.GetProperty("reason").GetString()}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"Foundry run '{runId}' exceeded five minutes. Inspect or cancel it in Foundry; it may still be running.");
        }
    }

    public static void ValidateOutput(JsonElement output)
    {
        if (!output.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array ||
            !output.TryGetProperty("has_more", out var hasMore) || hasMore.ValueKind != JsonValueKind.False ||
            data.GetArrayLength() != 1)
        {
            throw new InvalidOperationException("Expected exactly one Foundry evaluation output item.");
        }

        var item = data[0];
        if (!item.TryGetProperty("status", out var status) || status.GetString() != "completed")
        {
            throw new InvalidOperationException("The Foundry evaluation item did not produce a usable result.");
        }
        if (!item.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() != 1 ||
            !results[0].TryGetProperty("name", out var name) || name.GetString() != "similarity" ||
            (results[0].TryGetProperty("status", out var resultStatus) && resultStatus.GetString() != "completed") ||
            !results[0].TryGetProperty("score", out var score) ||
            score.ValueKind != JsonValueKind.Number || !score.TryGetDouble(out var value) ||
            !double.IsFinite(value) || value is < 1 or > 5 ||
            !results[0].TryGetProperty("reason", out var reason) ||
            reason.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reason.GetString()))
        {
            throw new InvalidOperationException("Expected one numeric Foundry similarity score from 1 to 5.");
        }
    }

    private static string ReadString(ClientResult result, string property)
    {
        using var document = JsonDocument.Parse(result.GetRawResponse().Content.ToMemory());
        return document.RootElement.GetProperty(property).GetString()
            ?? throw new InvalidOperationException($"Foundry response is missing '{property}'.");
    }
}

#pragma warning restore OPENAI001
