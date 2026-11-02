using System.Text.Json;
using AgenticDesignPatterns.TestSupport;
using Chapter05.ToolUse;
using Chapter05.ToolUse.Hosted;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.ToolUse.Tests;

public sealed class ToolUseTests
{
    [Theory]
    [InlineData(CalculatorOperation.Add, 5, 7, 12)]
    [InlineData(CalculatorOperation.Subtract, 5, 7, -2)]
    [InlineData(CalculatorOperation.Multiply, 12, 3, 36)]
    [InlineData(CalculatorOperation.Divide, 9, 2, 4.5)]
    [InlineData(CalculatorOperation.Factorial, 10, 0, 3628800)]
    [InlineData(CalculatorOperation.Factorial, 0, 0, 1)]
    public void Calculator_performs_typed_operations(CalculatorOperation operation, decimal left, decimal right, decimal expected) =>
        Assert.Equal(expected, new ToolUseFunctions().Calculate(operation, left, right));

    [Theory]
    [InlineData(-1)]
    [InlineData(28)]
    [InlineData(2.5)]
    public void Factorial_is_bounded(decimal input) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ToolUseFunctions().Calculate(CalculatorOperation.Factorial, input));

    [Fact]
    public void Calculator_surfaces_invalid_operations_and_arithmetic_failures()
    {
        var functions = new ToolUseFunctions();
        Assert.Throws<DivideByZeroException>(() => functions.Calculate(CalculatorOperation.Divide, 1, 0));
        Assert.Throws<OverflowException>(() => functions.Calculate(CalculatorOperation.Multiply, decimal.MaxValue, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => functions.Calculate((CalculatorOperation)99, 1));
    }

    [Fact]
    public void Lookup_tools_keep_their_sample_data_and_failures()
    {
        var functions = new ToolUseFunctions();
        Assert.Equal("The capital of France is Paris.", functions.SearchInformation("capital of France"));
        Assert.Contains("Simulated search result", functions.SearchInformation("dogs"));
        Assert.Equal(178.15m, functions.GetStockPrice("aapl"));
        Assert.Throws<KeyNotFoundException>(() => functions.GetStockPrice("UNKNOWN"));
    }

    [Theory]
    [InlineData("search: capital of France", "search_information", """{"query":"capital of France"}""", "The capital of France is Paris.")]
    [InlineData("calculate: 10!", "calculate", """{"operation":"Factorial","left":10,"right":0}""", "3628800")]
    [InlineData("stock: MSFT", "get_stock_price", """{"ticker":"MSFT"}""", "425.50")]
    public async Task Agent_selects_a_tool_and_returns_its_actual_result(
        string request, string tool, string arguments, string expected)
    {
        using var client = CreateToolClient(tool, arguments, [expected]);
        Assert.Contains(expected, await ToolUseAgent.RunAsync(client, request));
    }

    [Fact]
    public async Task Compound_sample_math_chains_tool_results()
    {
        var calls = 0;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            Assert.Equal("calculate", Assert.Single(options!.Tools!).Name);
            var results = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
            if (calls++ == 0)
            {
                return ToolCall("add", "calculate", """{"operation":"Add","left":5,"right":7}""");
            }

            Assert.Equal("12", results[0].Result?.ToString());
            return calls == 2
                ? ToolCall("multiply", "calculate", """{"operation":"Multiply","left":12,"right":3}""")
                : new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Result: {results.Last().Result}"));
        });
        Assert.Equal("Result: 36", await ToolUseAgent.RunAsync(client, "calculate: (5 + 7) * 3"));
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Unprefixed_input_preserves_all_tool_choices()
    {
        using var client = CreateToolClient("calculate",
            """{"operation":"Factorial","left":10,"right":0}""", ["3628800"],
            "search_information", "get_stock_price");
        Assert.Contains("3628800", await ToolUseAgent.RunAsync(client, "10!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("search: ")]
    [InlineData("calculate: ")]
    [InlineData("stock: ")]
    public async Task Empty_requests_fail_without_model_calls(string request)
    {
        using var client = new ScriptedChatClient((_, _) => throw new Xunit.Sdk.XunitException("Unexpected model call."));
        await Assert.ThrowsAsync<ArgumentException>(() => new ToolUseHostedAgent(client).RunAsync(request));
    }

    [Fact]
    public async Task Hosted_responses_returns_the_callers_result()
    {
        using var model = CreateToolClient("search_information", """{"query":"caller-specific topic"}""",
            ["Simulated search result for 'caller-specific topic': No specific information found, but the topic seems interesting."]);
        await using var host = await HostedResponses.StartAsync(new ToolUseHostedAgent(model));
        using var response = await host.InvokeAsync("search: caller-specific topic", "chapter05-tool-use");
        Assert.Equal("completed", response.RootElement.GetProperty("status").GetString());
        Assert.Contains("caller-specific topic", HostedResponses.OutputText(response.RootElement));
    }

    private static ScriptedChatClient CreateToolClient(
        string tool, string arguments, string[] expectedResults, params string[] otherTools)
    {
        var calls = 0;
        return new ScriptedChatClient((messages, options) =>
        {
            Assert.Equal(otherTools.Append(tool).Order(StringComparer.Ordinal),
                options!.Tools!.Select(item => item.Name).Order(StringComparer.Ordinal));
            if (calls++ == 0)
            {
                return ToolCall("call-1", tool, arguments);
            }

            var result = Assert.Single(messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>());
            Assert.Contains(Convert.ToString(result.Result, System.Globalization.CultureInfo.InvariantCulture), expectedResults);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Tool result: {result.Result}"));
        });
    }

    private static ChatResponse ToolCall(string id, string tool, string arguments) =>
        new(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent(id, tool, JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments))]));
}
