using Azure;
using Azure.AI.ContentSafety;
using Microsoft.Extensions.Configuration;

namespace Chapter18.Guardrails.Demos;

public static class ContentSafetyDemo
{
    public const int BlockSeverity = 4;

    public static async Task RunAsync(
        IConfiguration configuration,
        string? text = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = configuration["Foundry:ContentSafetyEndpoint"];
        var apiKey = configuration["Foundry:ContentSafetyApiKey"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var contentSafetyEndpoint) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Configure Foundry:ContentSafetyEndpoint and Foundry:ContentSafetyApiKey in shared user-secrets " +
                "or Foundry__ContentSafetyEndpoint and Foundry__ContentSafetyApiKey environment variables.");
        }

        var sample = string.IsNullOrWhiteSpace(text)
            ? "Summarize the benefits of unit testing for an API."
            : text;
        var client = new ContentSafetyClient(contentSafetyEndpoint, new AzureKeyCredential(apiKey));
        var response = await client.AnalyzeTextAsync(new AnalyzeTextOptions(sample), cancellationToken);
        var result = response.Value;
        var severities = result.CategoriesAnalysis.Select(
            category => category.Severity
                ?? throw new InvalidOperationException(
                    $"Content Safety did not return a severity for category '{category.Category}'."));
        var shouldBlock = ShouldBlock(severities);

        Console.WriteLine($"Text: {sample}");
        Console.WriteLine($"Block threshold: {BlockSeverity} (medium)");
        foreach (var category in result.CategoriesAnalysis)
        {
            Console.WriteLine($"{category.Category}: severity {category.Severity}");
        }

        Console.WriteLine($"Decision: {(shouldBlock ? "BLOCK" : "ALLOW")}");
    }

    public static bool ShouldBlock(IEnumerable<int> severities)
    {
        ArgumentNullException.ThrowIfNull(severities);
        return severities.Any(severity => severity >= BlockSeverity);
    }
}
