using System.Text;
using AgenticDesignPatterns.Shared.Hosting;
using Chapter08.Memory.Demos;

namespace Chapter08.Memory.Hosted;

public sealed class MemoryDemoAgent : StatelessWorkflowAgent
{
    public override string Name => "chapter08-memory";
    public override string Description =>
        "Stores a synthetic travel preference in Foundry-managed memory and answers the caller's recall question from a new MAF session.";

    protected override async Task<string> RunAsync(string input, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        output.AppendLine("Scripted demo: a synthetic fact is stored and your message is asked as the recall question. Each invocation uses an isolated temporary memory scope.");
        await FoundryMemoryDemo.RunAsync(
            writeOutput: line => output.AppendLine(line),
            cancellationToken: cancellationToken,
            useTemporaryScope: true,
            recallQuestion: input);
        return output.ToString();
    }
}
