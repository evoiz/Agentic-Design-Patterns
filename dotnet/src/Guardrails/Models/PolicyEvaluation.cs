using System.Text.Json.Serialization;

namespace Guardrails.Models;

[JsonConverter(typeof(ComplianceStatusConverter))]
public enum ComplianceStatus
{
    [JsonStringEnumMemberName("compliant")]
    Compliant,
    [JsonStringEnumMemberName("non-compliant")] NonCompliant,
    [JsonStringEnumMemberName("requires-review")] RequiresReview
}

public sealed class ComplianceStatusConverter() : JsonStringEnumConverter<ComplianceStatus>(allowIntegerValues: false);

public sealed record PolicyEvaluation
{
    [JsonPropertyName("compliance_status")]
    public required ComplianceStatus ComplianceStatus { get; init; }

    [JsonPropertyName("evaluation_summary")]
    public required string EvaluationSummary { get; init; }

    [JsonPropertyName("triggered_policies")]
    public required List<string> TriggeredPolicies { get; init; }
}
