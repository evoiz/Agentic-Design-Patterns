using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Chapter05.ToolUse;

[JsonConverter(typeof(JsonStringEnumConverter<CalculatorOperation>))]
public enum CalculatorOperation { Add, Subtract, Multiply, Divide, Factorial }

public sealed class ToolUseFunctions(Action<string>? writeToolOutput = null)
{
    private static readonly IReadOnlyDictionary<string, string> SearchResults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["weather in london"] = "The weather in London is currently cloudy with a temperature of 15°C.",
            ["capital of france"] = "The capital of France is Paris.",
            ["population of earth"] = "The estimated population of Earth is around 8 billion people.",
            ["tallest mountain"] = "Mount Everest is the tallest mountain above sea level."
        };

    private static readonly IReadOnlyDictionary<string, decimal> StockPrices =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["AAPL"] = 178.15m,
            ["GOOGL"] = 1750.30m,
            ["MSFT"] = 425.50m
        };

    [Description("Looks up factual information in a small deterministic sample search index, not current events.")]
    public string SearchInformation([Description("The topic or question to look up.")] string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var result = SearchResults.TryGetValue(query.Trim(), out var found)
            ? found
            : $"Simulated search result for '{query.Trim()}': No specific information found, but the topic seems interesting.";
        return Report("search_information", query, result);
    }

    [Description("Performs one typed decimal arithmetic operation. Chain calls for compound expressions. Factorial uses left only, an integer from 0 to 27. Never executes code.")]
    public decimal Calculate(
        CalculatorOperation operation,
        [Description("First operand, or factorial input.")] decimal left,
        [Description("Second operand; ignored for factorial.")] decimal right = 0)
    {
        var result = operation switch
        {
            CalculatorOperation.Add => left + right,
            CalculatorOperation.Subtract => left - right,
            CalculatorOperation.Multiply => left * right,
            CalculatorOperation.Divide => left / right,
            CalculatorOperation.Factorial => Factorial(left),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        return Report("calculate", $"{operation}({left}, {right})", result);
    }

    [Description("Looks up a simulated stock price for AAPL, GOOGL, or MSFT. Sample data, not a live quote.")]
    public decimal GetStockPrice([Description("The stock ticker symbol to look up.")] string ticker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);
        var normalized = ticker.Trim().ToUpperInvariant();
        return Report("get_stock_price", normalized, StockPrices.TryGetValue(normalized, out var price)
            ? price
            : throw new KeyNotFoundException($"Simulated price for ticker '{normalized}' not found."));
    }

    private static decimal Factorial(decimal input)
    {
        if (input < 0 || input > 27 || input != decimal.Truncate(input))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Factorial requires an integer from 0 to 27.");
        }

        var result = 1m;
        for (var value = 2; value <= input; value++)
        {
            result *= value;
        }

        return result;
    }

    private T Report<T>(string tool, string input, T result)
    {
        writeToolOutput?.Invoke($"Tool called: {tool}\nInput: {input}\nResult: {Convert.ToString(result, CultureInfo.InvariantCulture)}");
        return result;
    }
}
