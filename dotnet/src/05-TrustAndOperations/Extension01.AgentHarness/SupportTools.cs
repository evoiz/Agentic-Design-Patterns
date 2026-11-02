using System.ComponentModel;
using Extension01.AgentHarness.Models;

namespace Extension01.AgentHarness;

public sealed class SupportTools(Action<string>? writeStep = null)
{
    private readonly List<SupportEvidence> _evidence = [];
    private readonly object _gate = new();

    public IReadOnlyList<SupportEvidence> Evidence
    {
        get { lock (_gate) { return _evidence.ToArray(); } }
    }

    [Description("Read synthetic service health for checkout or identity. This does not query production.")]
    public string GetServiceStatus(string service) => Record(service, "status", service switch
    {
        "checkout" => "Synthetic snapshot: p95 latency 2400 ms; error rate 2%; database pool saturation 95%.",
        "identity" => "Synthetic snapshot: p95 latency 120 ms; error rate 0.1%; no active incident.",
        _ => throw new ArgumentException("Supported services are checkout and identity.", nameof(service))
    });

    [Description("Read the approved sample runbook for checkout or identity. Recommendations only; no actions are executed.")]
    public string ReadRunbook(string service) => Record(service, "runbook", service switch
    {
        "checkout" => "CHECKOUT-01: Compare latency and database pool metrics; inspect recent releases. " +
            "Recommend escalation to the database on-call when pool saturation exceeds 90%. " +
            "Restarts and rollbacks require an authorized operator; never execute them here.",
        "identity" => "IDENTITY-01: Compare authentication latency and error rate. " +
            "If healthy, request timestamps and correlation IDs, not passwords or tokens. " +
            "Configuration changes require an authorized operator.",
        _ => throw new ArgumentException("Supported services are checkout and identity.", nameof(service))
    });

    private string Record(string service, string kind, string value)
    {
        lock (_gate)
        {
            _evidence.Add(new SupportEvidence(service, kind, value));
            writeStep?.Invoke($"--- Tool: {service}/{kind} ---\n{value}");
        }

        return value;
    }
}
