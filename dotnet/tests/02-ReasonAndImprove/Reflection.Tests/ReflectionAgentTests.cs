using System.Collections.Concurrent;
using AgenticDesignPatterns.TestSupport;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter04.Reflection;
using Chapter04.Reflection.Hosted;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using Xunit;

namespace AgenticDesignPatterns.Reflection.Tests;

public sealed class ReflectionAgentTests
{
    public static IEnumerable<object[]> RoundCases()
    {
        foreach (var scenario in Enum.GetValues<ReflectionScenario>())
        {
            yield return [scenario, 1, 1, true];
            yield return [scenario, 1, 1, false];
            yield return [scenario, 2, 1, true];
            yield return [scenario, 2, 2, true];
            yield return [scenario, 2, 2, false];
            yield return [scenario, 3, 3, true];
            yield return [scenario, 3, 3, false];
            yield return [scenario, 4, 4, true];
            yield return [scenario, 4, 4, false];
        }
    }

    [Theory]
    [MemberData(nameof(RoundCases))]
    public async Task Every_scenario_uses_the_same_bounded_graph(
        ReflectionScenario scenario, int maxRounds, int rounds, bool accepted)
    {
        var reviews = Enumerable.Range(1, rounds)
            .Select(round => ReviewJson(accepted && round == rounds ? ReflectionDecision.Accept : ReflectionDecision.Revise));
        using var client = new ScriptedChatClient(reviews);
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, scenario);
        var stages = new List<string>();

        var result = await ReflectionWorkflow.RunAsync(
            generator, verifier, "Unique caller task.", new(maxRounds), stages.Add);

        Assert.Equal(accepted ? ReflectionOutcome.Accepted : ReflectionOutcome.MaxRoundsReached, result.Outcome);
        Assert.Equal(rounds, result.RoundsUsed);
        Assert.Equal(maxRounds, result.MaxRounds);
        Assert.Equal($"Draft {rounds}.", result.Draft);
        Assert.Equal(accepted ? ReflectionDecision.Accept : ReflectionDecision.Revise, result.Review.Decision);
        Assert.Equal(rounds * 2, client.Requests.Count);
        Assert.Equal(rounds * 2, stages.Count);
        for (var round = 1; round <= rounds; round++)
        {
            var generation = client.Requests[(round - 1) * 2];
            var verification = client.Requests[(round - 1) * 2 + 1];
            Assert.False(generation.IsReview);
            Assert.True(verification.IsReview);
            Assert.Contains("Unique caller task.", generation.Input);
            Assert.Contains("Unique caller task.", verification.Input);
            Assert.Contains($"Draft {round}.", verification.Input);
            if (round > 1)
            {
                Assert.Contains($"Draft {round - 1}.", generation.Input);
                Assert.Contains("Specific critique.", generation.Input);
                Assert.Contains("Independent review:", generation.Input);
            }
        }

        Assert.Contains(accepted ? "Outcome: Accepted" : "this draft was not accepted", ReflectionDemos.FormatResult(result));
    }

    [Fact]
    public async Task Factory_builds_a_real_feedback_graph_with_one_terminal_output()
    {
        using var client = new ScriptedChatClient([ReviewJson(ReflectionDecision.Revise), ReviewJson(ReflectionDecision.Accept)]);
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.ProductDescription);
        var workflow = ReflectionWorkflow.Create(generator, verifier);

        await using var run = await InProcessExecution.RunAsync(workflow, new ReflectionState("A reusable mug.", 3));

        Assert.DoesNotContain(run.OutgoingEvents, item => item is ExecutorFailedEvent or WorkflowErrorEvent);
        var terminal = Assert.Single(run.OutgoingEvents.OfType<WorkflowOutputEvent>());
        var result = Assert.IsType<ReflectionResult>(terminal.As<ReflectionResult>());
        Assert.Equal(2, result.RoundsUsed);
        Assert.Equal(ReflectionOutcome.Accepted, result.Outcome);
        Assert.Equal(4, client.Requests.Count);
    }

    [Fact]
    public async Task Default_budget_is_three_rounds()
    {
        using var client = new ScriptedChatClient(Enumerable.Repeat(ReviewJson(ReflectionDecision.Revise), 3));
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);

        var result = await ReflectionWorkflow.RunAsync(generator, verifier, "Photosynthesis.");

        Assert.Equal(ReflectionOutcome.MaxRoundsReached, result.Outcome);
        Assert.Equal(3, result.RoundsUsed);
        Assert.Equal(6, client.Requests.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_budgets_fail_before_model_access(int maxRounds)
    {
        using var client = new ScriptedChatClient();
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ReflectionWorkflow.RunAsync(generator, verifier, "Task.", new(maxRounds)));

        Assert.Empty(client.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Empty_tasks_fail_before_model_access(string task)
    {
        using var client = new ScriptedChatClient();
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);

        await Assert.ThrowsAsync<ArgumentException>(() => ReflectionWorkflow.RunAsync(generator, verifier, task));

        Assert.Empty(client.Requests);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"reasoning":"Missing decision."}""")]
    [InlineData("""{"decision":null,"reasoning":"Missing decision."}""")]
    [InlineData("""{"decision":"Unknown","reasoning":"Invalid decision."}""")]
    [InlineData("""{"decision":17,"reasoning":"Undefined enum."}""")]
    [InlineData("""{"decision":0,"reasoning":"Numeric enums are not review decisions."}""")]
    [InlineData("""{"decision":"0","reasoning":"Numeric enum strings are not review decisions."}""")]
    [InlineData("""{"decision":"Accept","reasoning":""}""")]
    [InlineData("""{"decision":"Revise","reasoning":null}""")]
    public async Task Invalid_reviews_fail_instead_of_accepting_or_retrying(string review)
    {
        using var client = new ScriptedChatClient([review]);
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);

        await Assert.ThrowsAnyAsync<Exception>(() => ReflectionWorkflow.RunAsync(generator, verifier, "Task."));

        Assert.Equal(2, client.Requests.Count);
    }

    [Fact]
    public async Task Empty_draft_fails_before_verification()
    {
        using var client = new ScriptedChatClient(draft: " ");
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => ReflectionWorkflow.RunAsync(generator, verifier, "Task."));

        Assert.Contains("empty draft", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Single(client.Requests);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Provider_failures_are_not_terminal_successes(int failingCall)
    {
        using var client = new ScriptedChatClient(beforeResponse: (call, _) =>
            call == failingCall ? Task.FromException(new InvalidOperationException("Provider unavailable.")) : Task.CompletedTask);
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => ReflectionWorkflow.RunAsync(generator, verifier, "Task."));

        Assert.Contains("Provider unavailable.", exception.ToString());
        Assert.Equal(failingCall, client.Requests.Count);
    }

    [Fact]
    public async Task Precancelled_run_makes_no_model_calls()
    {
        using var client = new ScriptedChatClient();
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReflectionWorkflow.RunAsync(generator, verifier, "Task.", cancellationToken: cancellation.Token));

        Assert.Empty(client.Requests);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Cancellation_during_either_agent_stops_the_run(int cancelCall)
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new ScriptedChatClient(beforeResponse: (call, token) =>
        {
            if (call == cancelCall)
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }

            return Task.CompletedTask;
        });
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReflectionWorkflow.RunAsync(generator, verifier, "Task.", cancellationToken: cancellation.Token));

        Assert.Equal(cancelCall, client.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_between_stages_prevents_the_next_model_call(bool afterReview)
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new ScriptedChatClient(Enumerable.Repeat(ReviewJson(ReflectionDecision.Revise), 3));
        var (generator, verifier) = ReflectionDemos.CreateAgents(client, ReflectionScenario.FactCheck);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReflectionWorkflow.RunAsync(
                generator, verifier, "Task.",
                writeOutput: stage =>
                {
                    if (stage.Contains(afterReview ? "Independent verification" : "Generated draft", StringComparison.Ordinal))
                    {
                        cancellation.Cancel();
                    }
                },
                cancellationToken: cancellation.Token));

        Assert.Equal(afterReview ? 2 : 1, client.Requests.Count);
    }

    [Theory]
    [InlineData("fact-check:", "fact-checker")]
    [InlineData("FACT-CHECK:", "fact-checker")]
    [InlineData("product:", "product description")]
    [InlineData("PrOdUcT:", "product description")]
    [InlineData("code-review:", "Python code reviewer")]
    [InlineData("CODE-REVIEW:", "Python code reviewer")]
    [InlineData("", "product description")]
    public async Task Host_preserves_prefix_routing_and_forwards_caller_task(string prefix, string reviewerInstructions)
    {
        using var client = new ScriptedChatClient([ReviewJson(ReflectionDecision.Revise), ReviewJson(ReflectionDecision.Accept)]);
        var agent = new ReflectionAgent(client);

        var response = await agent.RunAsync($"{prefix} Write a Python Fibonacci function with a docstring.");

        Assert.Contains("Outcome: Accepted", response.Text);
        Assert.Equal(4, client.Requests.Count);
        Assert.All(client.Requests, request =>
        {
            Assert.Contains("Fibonacci", request.Input);
            Assert.DoesNotContain("factorial", request.Input, StringComparison.OrdinalIgnoreCase);
        });
        Assert.All(client.Requests.Where(request => request.IsReview), request =>
            Assert.Contains(reviewerInstructions, request.Instructions, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("fact-check: ")]
    [InlineData("product: ")]
    [InlineData("code-review: ")]
    [InlineData(" ")]
    public async Task Host_rejects_empty_input(string input)
    {
        using var client = new ScriptedChatClient();
        await Assert.ThrowsAsync<ArgumentException>(() => new ReflectionAgent(client).RunAsync(input));
        Assert.Empty(client.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repeated_host_requests_have_isolated_state(bool concurrent)
    {
        using var client = new ScriptedChatClient(beforeResponse: (_, token) => Task.Delay(10, token));
        var agent = new ReflectionAgent(client);
        if (concurrent)
        {
            await Task.WhenAll(agent.RunAsync("product: TaskAlpha."), agent.RunAsync("fact-check: TaskBeta."));
        }
        else
        {
            await agent.RunAsync("product: TaskAlpha.");
            await agent.RunAsync("fact-check: TaskBeta.");
        }

        Assert.Equal(4, client.Requests.Count);
        Assert.Equal(2, client.Requests.Count(request => request.Input.Contains("TaskAlpha.", StringComparison.Ordinal)));
        Assert.Equal(2, client.Requests.Count(request => request.Input.Contains("TaskBeta.", StringComparison.Ordinal)));
        Assert.DoesNotContain(client.Requests, request =>
            request.Input.Contains("TaskAlpha.", StringComparison.Ordinal) && request.Input.Contains("TaskBeta.", StringComparison.Ordinal));
        Assert.DoesNotContain(client.Requests, request => request.Input.Contains("Previous draft:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Streaming_adapter_buffers_one_completed_workflow_result()
    {
        using var client = new ScriptedChatClient([ReviewJson(ReflectionDecision.Revise), ReviewJson(ReflectionDecision.Accept)]);
        var updates = new List<string>();
        await foreach (var update in new ReflectionAgent(client).RunStreamingAsync("product: A mug."))
        {
            Assert.Equal(4, client.Requests.Count);
            updates.Add(update.Text);
        }

        Assert.Contains("Outcome: Accepted", Assert.Single(updates));
    }

    [Theory]
    [InlineData("fact-check:", true)]
    [InlineData("product:", true)]
    [InlineData("code-review:", true)]
    [InlineData("fact-check:", false)]
    [InlineData("product:", false)]
    [InlineData("code-review:", false)]
    public async Task Responses_protocol_returns_visible_acceptance_or_exhaustion(string prefix, bool accepted)
    {
        using var model = new ScriptedChatClient(
            Enumerable.Repeat(ReviewJson(accepted ? ReflectionDecision.Accept : ReflectionDecision.Revise), 3));
        await using var host = await HostedResponses.StartAsync(new ReflectionAgent(model));
        using var body = await host.InvokeAsync($"{prefix} Caller-supplied subject.", "chapter04-reflection");
        Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
        var responseText = HostedResponses.OutputText(body.RootElement);
        Assert.All(model.Requests, request => Assert.Contains("Caller-supplied subject.", request.Input));
        Assert.Contains(accepted ? "Outcome: Accepted" : "Outcome: MaxRoundsReached", responseText);
        Assert.Equal(accepted ? 2 : 6, model.Requests.Count);
        if (!accepted)
        {
            Assert.Contains("this draft was not accepted", responseText);
        }
    }

    private static string ReviewJson(ReflectionDecision decision) =>
        JsonSerializer.Serialize(new ReflectionReview { Decision = decision, Reasoning = "Specific critique." });

    private sealed record ModelRequest(string Instructions, string Input, bool IsReview);

    private sealed class ScriptedChatClient(
        IEnumerable<string>? reviews = null,
        Func<int, CancellationToken, Task>? beforeResponse = null,
        string? draft = null) : IChatClient
    {
        private readonly ConcurrentQueue<ModelRequest> _requests = new();
        private readonly ConcurrentQueue<string> _reviews = new(reviews ?? []);
        private int _calls;
        private int _drafts;

        public IReadOnlyList<ModelRequest> Requests => _requests.ToArray();

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageList = messages.ToList();
            var input = messageList.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            var instructions = options?.Instructions
                ?? string.Join('\n', messageList.Where(message => message.Role == ChatRole.System).Select(message => message.Text));
            var isReview = instructions.Contains("Return structured output", StringComparison.Ordinal);
            _requests.Enqueue(new(instructions, input, isReview));
            var call = Interlocked.Increment(ref _calls);
            if (beforeResponse is not null)
            {
                await beforeResponse(call, cancellationToken);
            }

            var text = isReview
                ? _reviews.TryDequeue(out var review) ? review : ReviewJson(ReflectionDecision.Accept)
                : draft ?? $"Draft {Interlocked.Increment(ref _drafts)}.";
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
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
