using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter19.Evaluation.Demos;

public static class BasicMetricsDemo
{
    public static double ExactMatch(string response, string reference) =>
        string.Equals(response.Trim(), reference.Trim(), StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;

    public static async Task RunAsync(IChatClient chatClient, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n--- Exact match: notebook paraphrase ---");
        Console.WriteLine($"Response: {EvaluationSamples.CapitalResponse}");
        Console.WriteLine($"Reference: {EvaluationSamples.CapitalReference}");
        Console.WriteLine($"Response accuracy: {ExactMatch(EvaluationSamples.CapitalResponse, EvaluationSamples.CapitalReference)}");
        Console.WriteLine("An exact-match score of 0 does not imply semantic incorrectness.");

        Console.WriteLine("\n--- Timed simulated tool ---");
        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        Console.WriteLine($"Tool call result: Result for get weather ({stopwatch.Elapsed.TotalMilliseconds:F2} ms)");

        Console.WriteLine("\n--- MAF response latency and provider token usage ---");
        ChatClientAgent agent = new(chatClient, name: "BasicResponseAgent",
            instructions: "Answer the user's question concisely in one sentence.");
        long inputTokens = 0;
        long outputTokens = 0;
        var completeUsage = true;
        foreach (var prompt in new[] { EvaluationSamples.CapitalQuery, "Tell me a joke." })
        {
            stopwatch.Restart();
            var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
            stopwatch.Stop();
            Console.WriteLine($"Input: {prompt}\nResponse: {response.Text}");
            Console.WriteLine($"Latency: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            if (response.Usage?.InputTokenCount is { } input && response.Usage.OutputTokenCount is { } output)
            {
                inputTokens += input;
                outputTokens += output;
                Console.WriteLine($"Provider tokens: input={input}, output={output}");
            }
            else
            {
                completeUsage = false;
                Console.WriteLine("Provider token usage unavailable; not estimated from word counts.");
            }
        }

        Console.WriteLine(completeUsage
            ? $"Total provider tokens: input={inputTokens}, output={outputTokens}"
            : $"Partial provider tokens: input={inputTokens}, output={outputTokens}; some calls omitted usage.");
    }
}
