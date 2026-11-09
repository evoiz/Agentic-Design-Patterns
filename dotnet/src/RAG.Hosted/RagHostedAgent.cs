using AgenticDesignPatterns.Shared.Hosting;
using RAG;
using Microsoft.Extensions.AI;

namespace RAG.Hosted;

public sealed class RagHostedAgent(IChatClient chatClient) : StatelessWorkflowAgent
{
    public override string Name => "chapter14-rag";
    public override string Description =>
        "Answers caller questions using retrieved passages from the Chapter 14 sample knowledge base.";

    protected override Task<string> RunAsync(string input, CancellationToken cancellationToken) =>
        RagAgent.RunAsync(chatClient, input, cancellationToken: cancellationToken);
}
