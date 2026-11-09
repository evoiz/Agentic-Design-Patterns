using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace ConferenceDemos;

public static class AgentDemo
{
    public const string WelcomeQuestion = "In one sentence, welcome a .NET developer to Update Conference in Prague.";
    public const string WeatherQuestion = "What is the weather like in Prague today?";

    public static async Task RunAsync(
        IChatClient client, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(output);
        var agent = new ChatClientAgent(
            client,
            name: "PragueAssistant",
            instructions: """
                You are a concise, friendly assistant for Update Conference in Prague.
                For Prague weather questions, call GetPragueWeather.
                Clearly label the weather as fixed demo data, not a live forecast.
                """,
            tools: [AIFunctionFactory.Create(GetPragueWeather, name: nameof(GetPragueWeather))]
            );

        foreach (var question in new[] { WelcomeQuestion, WeatherQuestion })
        {
            output.WriteLine($"User question: {question}");
            var response = await agent.RunAsync(question, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException("The Prague agent returned an empty response.");
            }
            output.WriteLine($"Agent: {response.Text}\n");
        }

        [Description("Get fixed demo weather for Prague. This is sample data, not a live forecast.")]
        string GetPragueWeather()
        {
            const string weather = "Fixed demo data, not a live forecast: Prague is cloudy, 6 C.";
            output.WriteLine($"Tool call: GetPragueWeather -> {weather}");
            return weather;
        }
    }
}
