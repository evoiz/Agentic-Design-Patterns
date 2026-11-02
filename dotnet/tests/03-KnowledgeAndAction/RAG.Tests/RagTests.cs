using System.Collections.Concurrent;
using System.Globalization;
using AgenticDesignPatterns.TestSupport;
using Chapter14.RAG;
using Chapter14.RAG.Hosted;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.RAG.Tests;

public sealed class RagTests
{
    [Fact]
    public void Native_search_preserves_unique_citation_urls_from_metadata()
    {
        var content = new TextContent("A sourced answer.")
        {
            Annotations =
            [
                new CitationAnnotation { Title = "Official source", Url = new Uri("https://example.com/source") },
                new CitationAnnotation { Title = "Duplicate", Url = new Uri("https://example.com/source") },
                new CitationAnnotation { Url = new Uri("https://example.com/another") },
                new CitationAnnotation { Title = "File without a URL" }
            ]
        };
        var response = new AgentResponse(new ChatMessage(ChatRole.Assistant, [content]));

        Assert.Equal(
            "A sourced answer.\n\nSources:\nOfficial source: https://example.com/source\nSource: https://example.com/another",
            FoundryNativeDemo.FormatResponse(response));
    }

    [Fact]
    public void Native_search_preserves_text_when_no_url_citations_are_available()
    {
        var response = new AgentResponse(new ChatMessage(ChatRole.Assistant, "An answer without URL citations."));

        Assert.Equal("An answer without URL citations.", FoundryNativeDemo.FormatResponse(response));
    }

    [Theory]
    [InlineData("all")]
    [InlineData("rag")]
    [InlineData("foundry-files")]
    [InlineData("foundry-web")]
    public void Mode_words_after_the_question_starts_remain_question_text(string word)
    {
        var options = RagCommandLineOptions.Parse(["Tell", "me", word, "about", "infrastructure"]);

        Assert.Equal("rag", options.Mode);
        Assert.Equal($"Tell me {word} about infrastructure", options.Question);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("rag")]
    [InlineData("foundry-files")]
    [InlineData("foundry-web")]
    public void Explicit_leading_mode_is_selected_without_consuming_question_words(string mode)
    {
        var options = RagCommandLineOptions.Parse([mode, "Tell", "me", "all", "about", "infrastructure"]);

        Assert.Equal(mode, options.Mode);
        Assert.Equal("Tell me all about infrastructure", options.Question);
    }

    [Fact]
    public void Deployment_option_does_not_reopen_mode_detection_after_question_text()
    {
        var options = RagCommandLineOptions.Parse(["Tell", "me", "--deployment", "fast", "all", "about", "infrastructure"]);

        Assert.Equal("rag", options.Mode);
        Assert.Equal("fast", options.DeploymentAlias);
        Assert.Equal("Tell me all about infrastructure", options.Question);
    }

    [Fact]
    public void Deployment_option_before_mode_preserves_explicit_mode_selection()
    {
        var options = RagCommandLineOptions.Parse(["--deployment", "fast", "foundry-files", "Summarize", "all", "documents"]);

        Assert.Equal("foundry-files", options.Mode);
        Assert.Equal("fast", options.DeploymentAlias);
        Assert.Equal("Summarize all documents", options.Question);
    }

    [Fact]
    public void Empty_arguments_preserve_the_default_demo()
    {
        var options = RagCommandLineOptions.Parse([]);

        Assert.Equal("rag", options.Mode);
        Assert.Null(options.DeploymentAlias);
        Assert.Equal("What did the president say about Justice Breyer?", options.Question);
    }

    [Theory]
    [InlineData("--deployment")]
    [InlineData("--deployment", " ")]
    [InlineData("--deployment", "fast", "--deployment", "quality")]
    public void Invalid_deployment_arguments_are_rejected(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => RagCommandLineOptions.Parse(args));
    }

    [Fact]
    public void Retrieval_ranks_the_relevant_source_for_the_notebook_questions()
    {
        var knowledgeBase = new RagKnowledgeBase();

        Assert.Equal(
            "SOTU-2022-BREYER",
            knowledgeBase.Retrieve("What did the president say about Justice Breyer?").First().DocumentId);
        Assert.Equal(
            "SOTU-2022-ECONOMY",
            knowledgeBase.Retrieve("What did the address say about the economy?").First().DocumentId);
        Assert.Equal(
            "SOTU-2022-INFRASTRUCTURE",
            knowledgeBase.Retrieve("How will roads and bridges be repaired?").Single().DocumentId);
    }

    [Fact]
    public void Retrieval_returns_no_context_for_unmatched_queries()
    {
        var knowledgeBase = new RagKnowledgeBase();

        Assert.Empty(knowledgeBase.Retrieve("unrelated astrophysics vocabulary"));
        Assert.Contains("No relevant information", knowledgeBase.Search("unrelated astrophysics vocabulary"));
    }

    [Fact]
    public async Task Agent_calls_retrieval_with_the_caller_question_and_returns_its_answer()
    {
        var client = new ScriptedSearchModel("What did the address say about the economy?");
        var response = await RagAgent.RunAsync(client.Client, "What did the address say about the economy?");

        Assert.Contains("[SOTU-2022-ECONOMY]", response);
        Assert.Contains("What did the address say about the economy?", client.Requests);
        Assert.Contains("SOTU-2022-ECONOMY", client.FunctionResults.Single());
    }

    [Fact]
    public async Task Hosted_responses_protocol_returns_a_grounded_answer_for_the_caller_input()
    {
        var model = new ScriptedSearchModel("What did the address say about the economy?");
        await using var host = await HostedResponses.StartAsync(new RagHostedAgent(model.Client));
        using var body = await host.InvokeAsync("What did the address say about the economy?", "chapter14-rag");

        Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
        Assert.Contains("[SOTU-2022-ECONOMY]", HostedResponses.OutputText(body.RootElement));
        Assert.Contains("What did the address say about the economy?", model.Requests);
    }
    [Fact]
    public async Task Agent_rejects_an_empty_question_before_calling_the_model()
    {
        var client = new ScriptedSearchModel("unused");

        await Assert.ThrowsAsync<ArgumentException>(() => RagAgent.RunAsync(client.Client, " "));

        Assert.Empty(client.Requests);
    }

    private sealed class ScriptedSearchModel
    {
        private readonly ConcurrentQueue<string> _requests = new();
        private readonly ConcurrentQueue<string> _functionResults = new();

        public ScriptedSearchModel(string searchQuery)
        {
            Client = new ScriptedChatClient((messages, options) =>
            {
                var messageList = messages.ToArray();
                _requests.Enqueue(messageList.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty);
                var functionResult = messageList.SelectMany(message => message.Contents).OfType<FunctionResultContent>().SingleOrDefault();
                if (functionResult is null)
                {
                    var searchTool = Assert.Single(options?.Tools ?? []);
                    Assert.Equal("search_knowledge_base", searchTool.Name);
                    return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                        [new FunctionCallContent("call-1", searchTool.Name, new Dictionary<string, object?> { ["query"] = searchQuery })]));
                }

                _functionResults.Enqueue(Convert.ToString(functionResult.Result, CultureInfo.InvariantCulture) ?? string.Empty);
                return new ChatResponse(new ChatMessage(
                    ChatRole.Assistant, "The address focused on lowering family costs [SOTU-2022-ECONOMY]."));
            });
        }

        public ScriptedChatClient Client { get; }
        public IReadOnlyList<string> Requests => _requests.ToArray();
        public IReadOnlyList<string> FunctionResults => _functionResults.ToArray();
    }
}