using System.Text.Json.Serialization;

namespace Chapter18.Guardrails.Models;

[JsonConverter(typeof(ComplianceStatusConverter))]
public enum ComplianceStatus
{
    [JsonStringEnumMemberName("compliant")]
    Compliant,
    [JsonStringEnumMemberName("non-compliant")]
    NonCompliant
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
