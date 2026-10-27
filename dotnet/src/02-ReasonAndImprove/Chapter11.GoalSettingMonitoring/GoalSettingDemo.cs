using System.Text;
using System.Text.RegularExpressions;
using Chapter11.GoalSettingMonitoring.Models;
using Microsoft.Extensions.AI;

namespace Chapter11.GoalSettingMonitoring;

public static partial class GoalSettingDemo
{
    public static async Task<string> RunAsync(
        IChatClient chatClient,
        GoalSettingRequest request,
        string? outputDirectory = null,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        writeOutput ??= Console.WriteLine;
        writeOutput($"Use case: {request.UseCase}");
        writeOutput("Goals:");
        foreach (var goal in request.Goals)
        {
            writeOutput($"- {goal}");
        }

        var result = await GoalSettingWorkflow.RunAsync(
            chatClient,
            request,
            writeOutput,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.Combine(outputDirectory ?? Environment.CurrentDirectory, "generated-code");
        Directory.CreateDirectory(directory);
        var slug = CreateSlug(request.UseCase);
        var filename = $"{slug}_{Guid.NewGuid():N}"[..(slug.Length + 9)] + ".py";
        var path = Path.Combine(directory, filename);
        await File.WriteAllTextAsync(
            path,
            CreateHeader(request.UseCase) + result.FinalCode + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        writeOutput("\n--- Goal-setting result ---");
        writeOutput(result.Format());
        writeOutput($"Saved generated code to: {path}");
        return path;
    }

    private static string CreateHeader(string useCase)
    {
        var commentLines = useCase
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
            .Select(line => $"# {line}");
        return $"# Generated Python program for the requested use case.{Environment.NewLine}" +
               $"# Caller use case:{Environment.NewLine}" +
               string.Join(Environment.NewLine, commentLines) +
               $"{Environment.NewLine}{Environment.NewLine}";
    }

    private static string CreateSlug(string value)
    {
        var slug = NonAlphaNumeric().Replace(value.ToLowerInvariant(), "_").Trim('_');
        return slug.Length == 0 ? "generated_code" : slug[..Math.Min(slug.Length, 24)];
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphaNumeric();
}
