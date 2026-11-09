using System.Text.Json;
using AgenticDesignPatterns.Shared.Hosting;
using GoalSettingMonitoring;
using GoalSettingMonitoring.Models;
using Microsoft.Extensions.AI;

namespace GoalSettingMonitoring.Hosted;

// Accepts plain text or JSON {useCase, goals, maxIterations, scenario}.
public sealed class GoalSettingAgent(IChatClient chatClient, bool enableSensitiveTelemetry = false)
    : StatelessWorkflowAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public override string Name => "chapter11-goal-setting-monitoring";
    public override string Description =>
        "Generates code and iterates against caller-defined success goals within a fixed budget.";

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken)
    {
        var result = await GoalSettingWorkflow.RunAsync(
            chatClient,
            ParseRequest(input),
            cancellationToken: cancellationToken,
            enableSensitiveTelemetry: enableSensitiveTelemetry);
        return result.Format();
    }

    private static GoalSettingRequest ParseRequest(string input)
    {
        var trimmed = input.Trim();
        GoalSettingRequest? request;
        try
        {
            request = trimmed.StartsWith('{')
                ? JsonSerializer.Deserialize<GoalSettingRequest>(trimmed, JsonOptions)
                : GoalSettingRequest.DotNetDeliverableSample with { UseCase = trimmed };
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "A structured request must be valid JSON with useCase, goals, and optional maxIterations and scenario.",
                nameof(input),
                exception);
        }

        if (request is null)
        {
            throw new ArgumentException("The structured request cannot be JSON null.", nameof(input));
        }

        request.Validate();
        return request;
    }
}
