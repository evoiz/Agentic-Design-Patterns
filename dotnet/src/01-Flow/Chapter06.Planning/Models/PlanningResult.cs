namespace Chapter06.Planning.Models;

public sealed record PlanningResult(IReadOnlyList<string> Steps, string Summary)
{
    public string Format() =>
        $"""
        ### Plan
        {string.Join(Environment.NewLine, Steps.Select(step => $"- {step}"))}

        ### Summary
        {Summary}
        """;
}
