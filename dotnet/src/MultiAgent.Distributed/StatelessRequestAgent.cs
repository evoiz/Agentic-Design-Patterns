using AgenticDesignPatterns.Shared.Hosting;

namespace MultiAgent.Distributed;

public sealed class StatelessRequestAgent(
    string name,
    Func<string, CancellationToken, Task<string>> run) : StatelessWorkflowAgent
{
    public override string Name => name;

    protected override Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
        run(input, cancellationToken);
}
