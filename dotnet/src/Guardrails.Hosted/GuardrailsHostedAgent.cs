using System.Text.Json;
using Guardrails;
using AgenticDesignPatterns.Shared.Hosting;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Guardrails.Hosted;

public sealed class GuardrailsHostedAgent(
    IChatClient chatClient,
    GuardrailPolicyVariant defaultVariant = GuardrailPolicyVariant.Conservative,
    bool enableSensitiveTelemetry = false) : StatelessWorkflowAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override string Name => "chapter18-guardrails";
    public override string Description =>
        "Screens caller requests with a structured policy evaluator before allowing the assistant to respond.";

    protected override async Task<string> RunAsync(string request, CancellationToken cancellationToken)
    {
        var parsed = ParseRequest(request, defaultVariant);
        var response = await GuardrailsAgent.Create(
                chatClient,
                variant: parsed.Variant,
                enableSensitiveTelemetry: enableSensitiveTelemetry)
            .RunAsync(parsed.Text, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException("The guarded assistant returned an empty response.");
        }

        return response.Text;
    }

    private static (string Text, GuardrailPolicyVariant Variant) ParseRequest(
        string input,
        GuardrailPolicyVariant defaultVariant)
    {
        if (!Enum.IsDefined(defaultVariant))
        {
            throw new ArgumentOutOfRangeException(nameof(defaultVariant));
        }

        var trimmed = input.Trim();
        if (trimmed.StartsWith('{'))
        {
            HostedGuardrailsRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<HostedGuardrailsRequest>(trimmed, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException(
                    "A structured request must be valid JSON with non-empty text and an optional policyVariant.",
                    nameof(input),
                    exception);
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("A structured request must include non-empty text.", nameof(input));
            }

            var variant = request.PolicyVariant ?? defaultVariant;
            if (!Enum.IsDefined(variant))
            {
                throw new ArgumentException("Select a supported policyVariant.", nameof(input));
            }

            return (request.Text, variant);
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Request text must be non-empty.", nameof(input));
        }

        return (trimmed, defaultVariant);
    }

    private sealed record HostedGuardrailsRequest(
        string? Text,
        GuardrailPolicyVariant? PolicyVariant);
}
