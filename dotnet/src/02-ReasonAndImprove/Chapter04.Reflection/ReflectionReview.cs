using System.Text.Json.Serialization;

namespace Chapter04.Reflection;

[JsonConverter(typeof(JsonStringEnumConverter<ReflectionDecision>))]
public enum ReflectionDecision
{
    Accept,
    Revise
}

public sealed record ReflectionReview
{
    public ReflectionDecision? Decision { get; init; }
    public string Reasoning { get; init; } = string.Empty;
}
