namespace GoalSettingMonitoring.Models;

public sealed record DotNetImplementationBrief(
    string? Title,
    string? Language,
    string? TargetFramework,
    string? Summary,
    IReadOnlyList<string>? ImplementationSteps);

public sealed record GoalSettingHardCriteriaResult(
    bool MeetsAll,
    IReadOnlyList<string> Issues);

public static class GoalSettingHardCriteria
{
    public const string RequiredLanguage = "C#";
    public const string RequiredTargetFramework = "net10.0";
    public const int MinimumImplementationSteps = 3;

    private static readonly IReadOnlyList<string> Criteria = Array.AsReadOnly(
    [
        $"Language is exactly {RequiredLanguage}.",
        $"Target framework is exactly {RequiredTargetFramework}.",
        $"The brief contains at least {MinimumImplementationSteps} non-empty implementation steps.",
        "The brief contains a non-empty title and summary."
    ]);

    public static IReadOnlyList<string> Descriptions => Criteria;

    public static GoalSettingHardCriteriaResult Check(
        string? generatedJson,
        out DotNetImplementationBrief? brief)
    {
        brief = null;
        var issues = new List<string>();
        try
        {
            brief = System.Text.Json.JsonSerializer.Deserialize<DotNetImplementationBrief>(
                generatedJson ?? string.Empty,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        }
        catch (System.Text.Json.JsonException)
        {
            issues.Add("Output must be one valid JSON object matching the .NET implementation brief shape.");
        }

        if (brief is null)
        {
            if (issues.Count == 0)
            {
                issues.Add("Output must be a non-null .NET implementation brief.");
            }

            return new GoalSettingHardCriteriaResult(false, Array.AsReadOnly(issues.ToArray()));
        }

        if (!string.Equals(brief.Language, RequiredLanguage, StringComparison.Ordinal))
        {
            issues.Add($"language must be exactly '{RequiredLanguage}'.");
        }

        if (!string.Equals(brief.TargetFramework, RequiredTargetFramework, StringComparison.Ordinal))
        {
            issues.Add($"targetFramework must be exactly '{RequiredTargetFramework}'.");
        }

        if (string.IsNullOrWhiteSpace(brief.Title) || string.IsNullOrWhiteSpace(brief.Summary))
        {
            issues.Add("title and summary must both be non-empty.");
        }

        if (brief.ImplementationSteps is null ||
            brief.ImplementationSteps.Count < MinimumImplementationSteps ||
            brief.ImplementationSteps.Any(string.IsNullOrWhiteSpace))
        {
            issues.Add(
                $"implementationSteps must contain at least {MinimumImplementationSteps} non-empty steps.");
        }

        return new GoalSettingHardCriteriaResult(issues.Count == 0, Array.AsReadOnly(issues.ToArray()));
    }
}
