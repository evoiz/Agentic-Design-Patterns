using System.Text.Json.Serialization;

namespace Chapter02.Routing.Models;

[JsonConverter(typeof(JsonStringEnumConverter<RoutingDecision>))]
internal enum RoutingDecision
{
    Booker,
    Info,
    Unclear
}

internal sealed record RoutingAssessment(RoutingDecision? Decision, string Rationale);
