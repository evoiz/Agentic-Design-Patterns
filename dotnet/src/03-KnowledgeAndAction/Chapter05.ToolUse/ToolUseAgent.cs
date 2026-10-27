using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter05.ToolUse;

public enum ToolUseScenario
{
    Search,
    Calculator,
    StockPrice
}

public static class ToolUseAgent
{
    public const string TelemetrySourceName = "Chapter05.ToolUse";

    public static AIAgent Create(
        IChatClient chatClient,
        ToolUseScenario? scenario = null,
        Action<string>? writeToolOutput = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        var functions = new ToolUseFunctions(writeToolOutput);
        var scenarioInstructions = scenario switch
        {
            ToolUseScenario.Search =>
                "Every answer must call search_information first; do not answer search requests from memory.",
            ToolUseScenario.Calculator =>
                "Every arithmetic request must call calculate_expression; do not calculate in your own reasoning.",
            ToolUseScenario.StockPrice =>
                "Every ticker price request must call get_stock_price; do not make up or infer a price.",
            _ => "Use a provided tool whenever the user asks for a lookup or calculation that it supports."
        };
        var tools = scenario switch
        {
            ToolUseScenario.Search =>
                new AITool[]
                {
                    AIFunctionFactory.Create(
                        (Func<string, string>)functions.SearchInformation,
                        name: "search_information")
                },
            ToolUseScenario.Calculator =>
                new AITool[]
                {
                    AIFunctionFactory.Create(
                        (Func<string, string>)functions.CalculateExpression,
                        name: "calculate_expression")
                },
            ToolUseScenario.StockPrice =>
                new AITool[]
                {
                    AIFunctionFactory.Create(
                        (Func<string, decimal>)functions.GetStockPrice,
                        name: "get_stock_price")
                },
            null =>
            [
                AIFunctionFactory.Create(
                    (Func<string, string>)functions.SearchInformation,
                    name: "search_information"),
                AIFunctionFactory.Create(
                    (Func<string, string>)functions.CalculateExpression,
                    name: "calculate_expression"),
                AIFunctionFactory.Create(
                    (Func<string, decimal>)functions.GetStockPrice,
                    name: "get_stock_price")
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown tool-use scenario.")
        };

        return new ChatClientAgent(
                chatClient,
                instructions: $"""
                    You are a helpful assistant with the tools explicitly provided for this request.
                    {scenarioInstructions}
                    Clearly identify simulated search results and stock prices as sample data, never as current facts.
                    Do not claim that the sample search index contains information it did not return.
                    """,
                name: "ToolUseAgent",
                description: "Selects and invokes a narrow function tool to answer the caller's request.",
                tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
    }

    public static async Task<string> RunAsync(
        IChatClient chatClient,
        string request,
        Action<string>? writeToolOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var (scenario, input) = ParseRequest(request);
        var agent = Create(chatClient, scenario, writeToolOutput);
        var response = await agent.RunAsync(input, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new InvalidOperationException("The tool-use agent returned an empty response.");
        }

        return response.Text;
    }

    public static (ToolUseScenario? Scenario, string Input) ParseRequest(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var prefixes = new (string Prefix, ToolUseScenario Scenario)[]
        {
            ("search:", ToolUseScenario.Search),
            ("calculate:", ToolUseScenario.Calculator),
            ("stock:", ToolUseScenario.StockPrice)
        };

        foreach (var (prefix, scenario) in prefixes)
        {
            if (request.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var input = request[prefix.Length..].Trim();
                ArgumentException.ThrowIfNullOrWhiteSpace(input);
                return (scenario, input);
            }
        }

        return (null, request.Trim());
    }
}
