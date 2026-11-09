using System.Text.Json.Serialization;

namespace Routing.Models;

[JsonConverter(typeof(JsonStringEnumConverter<RoutingDecision>))]
internal enum RoutingDecision
{
    Booker,
    Info,
    Unclear
}

internal sealed record RoutingAssessment(RoutingDecision? Decision, string Rationale);
