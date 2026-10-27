using System.ComponentModel;
using System.Globalization;

namespace Chapter05.ToolUse;

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

    [Description("Looks up factual information in a small deterministic sample search index. Use this for known sample topics, not current events.")]
    public string SearchInformation(
        [Description("The topic or question to look up.")] string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var normalizedQuery = query.Trim();
        var result = SearchResults.TryGetValue(normalizedQuery, out var found)
            ? found
            : $"Simulated search result for '{normalizedQuery}': No specific information found, but the topic seems interesting.";
        writeToolOutput?.Invoke($"Tool called: search_information\nQuery: {normalizedQuery}\nResult: {result}");
        return result;
    }

    [Description("Evaluates a basic arithmetic expression using a bounded, deterministic parser. Supports +, -, *, /, parentheses, decimals, and factorial with integer inputs from 0 to 27. Does not execute general code.")]
    public string CalculateExpression(
        [Description("An arithmetic expression, such as (5 + 7) * 3 or 10!.")] string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var result = new ArithmeticExpressionParser(expression).Parse();
        var formattedResult = result.ToString("G29", CultureInfo.InvariantCulture);
        writeToolOutput?.Invoke($"Tool called: calculate_expression\nExpression: {expression}\nResult: {formattedResult}");
        return formattedResult;
    }

    [Description("Looks up a simulated stock price for a supported ticker symbol (AAPL, GOOGL, or MSFT). This is sample data, not a live quote.")]
    public decimal GetStockPrice(
        [Description("The stock ticker symbol to look up.")] string ticker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        if (!StockPrices.TryGetValue(normalizedTicker, out var price))
        {
            throw new KeyNotFoundException($"Simulated price for ticker '{normalizedTicker}' not found.");
        }

        writeToolOutput?.Invoke(
            $"Tool called: get_stock_price\nTicker: {normalizedTicker}\nResult: {price.ToString("0.00", CultureInfo.InvariantCulture)}");
        return price;
    }

    private sealed class ArithmeticExpressionParser(string expression)
    {
        private const int MaximumExpressionLength = 128;
        private const int MaximumNestingDepth = 32;
        private const int MaximumFactorialInput = 27;
        private readonly string _expression = expression;
        private int _position;
        private int _nestingDepth;

        public decimal Parse()
        {
            if (_expression.Length > MaximumExpressionLength)
            {
                throw new FormatException($"Expressions cannot exceed {MaximumExpressionLength} characters.");
            }

            var result = ParseAddition();
            SkipWhitespace();
            if (_position != _expression.Length)
            {
                throw InvalidExpression();
            }

            return result;
        }

        private decimal ParseAddition()
        {
            var result = ParseMultiplication();
            while (TryConsume('+') || TryConsume('-'))
            {
                var operation = _expression[_position - 1];
                var right = ParseMultiplication();
                result = operation == '+' ? result + right : result - right;
            }

            return result;
        }

        private decimal ParseMultiplication()
        {
            var result = ParseUnary();
            while (TryConsume('*') || TryConsume('/'))
            {
                var operation = _expression[_position - 1];
                var right = ParseUnary();
                result = operation == '*' ? result * right : result / right;
            }

            return result;
        }

        private decimal ParseUnary()
        {
            if (TryConsume('+'))
            {
                return ParseUnary();
            }

            if (TryConsume('-'))
            {
                return -ParseUnary();
            }

            var result = ParsePrimary();
            if (TryConsume('!'))
            {
                if (result < 0 || result != decimal.Truncate(result) || result > MaximumFactorialInput)
                {
                    throw new FormatException($"Factorial requires an integer from 0 to {MaximumFactorialInput}.");
                }

                var factorial = 1m;
                for (var value = 2; value <= (int)result; value++)
                {
                    factorial *= value;
                }

                result = factorial;
            }

            return result;
        }

        private decimal ParsePrimary()
        {
            SkipWhitespace();
            if (TryConsume('('))
            {
                if (++_nestingDepth > MaximumNestingDepth)
                {
                    throw new FormatException($"Expressions cannot nest more than {MaximumNestingDepth} parentheses.");
                }

                var result = ParseAddition();
                if (!TryConsume(')'))
                {
                    throw InvalidExpression();
                }

                _nestingDepth--;
                return result;
            }

            var start = _position;
            while (_position < _expression.Length &&
                (char.IsAsciiDigit(_expression[_position]) || _expression[_position] == '.'))
            {
                _position++;
            }

            if (start == _position ||
                !decimal.TryParse(
                    _expression.AsSpan(start, _position - start),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                throw InvalidExpression();
            }

            return value;
        }

        private bool TryConsume(char expected)
        {
            SkipWhitespace();
            if (_position >= _expression.Length || _expression[_position] != expected)
            {
                return false;
            }

            _position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
            {
                _position++;
            }
        }

        private FormatException InvalidExpression() =>
            new($"Invalid arithmetic expression near position {_position}: '{_expression}'.");
    }
}
