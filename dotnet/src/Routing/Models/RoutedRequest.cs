namespace Routing.Models;

internal sealed record RoutedRequest(
    string OriginalRequest,
    RoutingDecision Decision);
