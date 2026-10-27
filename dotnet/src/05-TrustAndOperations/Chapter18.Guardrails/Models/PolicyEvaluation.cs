using System.Text.Json.Serialization;

namespace Chapter18.Guardrails.Models;

public sealed record PolicyEvaluation
{
    [JsonPropertyName("compliance_status")]
    public required string ComplianceStatus { get; init; }

    [JsonPropertyName("evaluation_summary")]
    public required string EvaluationSummary { get; init; }

    [JsonPropertyName("triggered_policies")]
    public required List<string> TriggeredPolicies { get; init; }
}
