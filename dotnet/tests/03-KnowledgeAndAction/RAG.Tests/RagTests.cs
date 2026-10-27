using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter14.RAG;
using Chapter14.RAG.Hosted;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.TestHost;
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
        using var client = new ScriptedChatClient("What did the address say about the economy?");
        var response = await RagAgent.RunAsync(client, "What did the address say about the economy?");

        Assert.Contains("[SOTU-2022-ECONOMY]", response);
        Assert.Contains("What did the address say about the economy?", client.Requests);
        Assert.Contains("SOTU-2022-ECONOMY", client.FunctionResults.Single());
    }

    [Fact]
    public async Task Hosted_responses_protocol_returns_a_grounded_answer_for_the_caller_input()
    {
        using var model = new ScriptedChatClient("What did the address say about the economy?");
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.Services.AddFoundryResponses(new RagHostedAgent(model));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();

        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter14-rag",
                input = "What did the address say about the economy?",
                stream = false,
                store = false
            });
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
            var responseText = string.Concat(
                body.RootElement.GetProperty("output").EnumerateArray()
                    .SelectMany(item => item.GetProperty("content").EnumerateArray())
                    .Where(item => item.GetProperty("type").GetString() == "output_text")
                    .Select(item => item.GetProperty("text").GetString()));
            Assert.Contains("[SOTU-2022-ECONOMY]", responseText);
            Assert.Contains("What did the address say about the economy?", model.Requests);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Agent_rejects_an_empty_question_before_calling_the_model()
    {
        using var client = new ScriptedChatClient("unused");

        await Assert.ThrowsAsync<ArgumentException>(() => RagAgent.RunAsync(client, " "));

        Assert.Empty(client.Requests);
    }

    private sealed class ScriptedChatClient(string searchQuery) : IChatClient
    {
        private readonly ConcurrentQueue<string> _requests = new();
        private readonly ConcurrentQueue<string> _functionResults = new();

        public IReadOnlyList<string> Requests => _requests.ToArray();
        public IReadOnlyList<string> FunctionResults => _functionResults.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageList = messages.ToArray();
            var userInput = messageList.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            _requests.Enqueue(userInput);

            if (_requests.Count == 1)
            {
                var searchTool = Assert.Single(options?.Tools ?? []);
                Assert.Equal("search_knowledge_base", searchTool.Name);
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                "call-1",
                                searchTool.Name,
                                new Dictionary<string, object?> { ["query"] = searchQuery })
                        ])));
            }

            var functionResult = messageList
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Single();
            var resultText = Convert.ToString(functionResult.Result, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            _functionResults.Enqueue(resultText);
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "The address focused on lowering family costs [SOTU-2022-ECONOMY].")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var message in response.Messages)
            {
                yield return new ChatResponseUpdate(message.Role, message.Text);
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
