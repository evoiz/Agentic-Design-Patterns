using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace A2A;

public static class InterAgentAgent
{
    public const string TelemetrySourceName = "A2A";
    public const string Name = "WeatherBot";
    public const string Description =
        "Answers weather-related questions with general guidance and clearly distinguishes it from live observations.";

    public static Uri ValidateEndpoint(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("The A2A agent URL must be an absolute HTTP or HTTPS URL.", nameof(value));
        }

        return endpoint;
    }

    public static AIAgent Create(IChatClient chatClient) =>
        new ChatClientAgent(
                chatClient,
                name: Name,
                description: Description,
                instructions:
                """
                You are WeatherBot, a demonstration agent reached over the Agent-to-Agent (A2A) protocol.
                Answer the caller's request clearly and helpfully. You do not have live weather,
                exchange-rate, or calendar data and must never invent current observations, rates,
                appointments, or completed actions. State when a request needs a live external service.
                """)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = false)
            .Build();
}
