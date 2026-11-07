using AgenticDesignPatterns.TestSupport;
using ConferenceDemos;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class GettingStartedTests
{
    [Fact]
    public async Task Agent_answers_the_welcome_then_invokes_the_actual_weather_tool()
    {
        var calls = 0;
        using var output = new StringWriter();
        using var client = new ScriptedChatClient((messages, options) =>
        {
            calls++;
            var tool = Assert.Single(options!.Tools!);
            Assert.Equal("GetPragueWeather", tool.Name);
            Assert.Contains("not a live forecast", tool.Description);
            Assert.Contains("fixed demo data", options.Instructions);
            var request = messages.ToArray();
            if (calls == 1)
            {
                Assert.Contains(request, m => m.Role == ChatRole.User && m.Text == AgentDemo.WelcomeQuestion);
                return Text("Welcome to Prague, .NET developers!");
            }
            Assert.Contains(request, m => m.Role == ChatRole.User && m.Text == AgentDemo.WeatherQuestion);
            if (calls == 2)
            {
                return new(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("weather-1", tool.Name, new Dictionary<string, object?>())]));
            }
            var result = Assert.Single(request.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
            Assert.Equal("weather-1", result.CallId);
            Assert.Equal("Fixed demo data, not a live forecast: Prague is cloudy, 6 C.", result.Result?.ToString());
            return Text($"Prague weather: {result.Result}");
        });

        await AgentDemo.RunAsync(client, output);

        Assert.Equal(3, calls);
        var transcript = output.ToString();
        Assert.Contains($"User question: {AgentDemo.WelcomeQuestion}", transcript);
        Assert.Contains($"User question: {AgentDemo.WeatherQuestion}", transcript);
        Assert.Contains("Agent: Welcome to Prague", transcript);
        Assert.Contains("Tool call: GetPragueWeather -> Fixed demo data, not a live forecast", transcript);
        Assert.Contains("Agent: Prague weather: Fixed demo data, not a live forecast", transcript);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Agent_rejects_empty_answers(string answer)
    {
        using var client = new ScriptedChatClient((_, _) => Text(answer));
        using var output = new StringWriter();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AgentDemo.RunAsync(client, output));
        Assert.Contains("empty response", error.Message);
        Assert.DoesNotContain("Agent:", output.ToString());
    }

    [Fact]
    public async Task Agent_propagates_inference_errors()
    {
        using var client = new ScriptedChatClient((_, _) => throw new HttpRequestException("Inference failed."));
        using var output = new StringWriter();
        await Assert.ThrowsAsync<HttpRequestException>(() => AgentDemo.RunAsync(client, output));
    }

    [Fact]
    public async Task Agent_cancellation_cannot_print_a_completed_answer()
    {
        using var cancellation = new CancellationTokenSource();
        using var output = new StringWriter();
        using var client = new ScriptedChatClient((_, _) =>
        {
            cancellation.Cancel();
            return Text("Must not be printed.");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AgentDemo.RunAsync(client, output, cancellation.Token));
        Assert.DoesNotContain("Agent:", output.ToString());
    }

    [Fact]
    public async Task Recommendation_workflow_routes_trimmed_input_and_emits_completion_and_output_events()
    {
        var events = new List<WorkflowEvent>();

        var answer = await WelcomeWorkflowDemo.RunAsync(" Alex ", onEvent: events.Add);

        Assert.Equal("Completed workflow.", answer);
        Assert.Equal(["GetAttendeeProfile", "RecommendSession", "CreateRecommendation"],
            events.OfType<ExecutorCompletedEvent>().Select(e => e.ExecutorId));
        Assert.Equal("Alex: .NET Developer interested in AI", events.OfType<ExecutorCompletedEvent>().First().Data);
        Assert.Equal("Building Agentic AI with Microsoft Foundry", events.OfType<ExecutorCompletedEvent>().ElementAt(1).Data);
        Assert.Equal("Recommended session: Building Agentic AI with Microsoft Foundry",
            Assert.Single(events.OfType<WorkflowOutputEvent>()).As<string>());
    }

    [Fact]
    public async Task Recommendation_workflow_has_a_default_attendee()
    {
        var events = new List<WorkflowEvent>();
        Assert.Equal("Completed workflow.", await WelcomeWorkflowDemo.RunAsync(onEvent: events.Add));
        Assert.Equal("Peter: .NET Developer interested in AI", events.OfType<ExecutorCompletedEvent>().First().Data);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Recommendation_workflow_surfaces_executor_validation_failures(string attendee)
    {
        var events = new List<WorkflowEvent>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WelcomeWorkflowDemo.RunAsync(attendee, onEvent: events.Add));
        Assert.Contains("GetAttendeeProfile", error.Message);
        Assert.Contains(events, e => e is ExecutorFailedEvent);
        Assert.Empty(events.OfType<WorkflowOutputEvent>());
    }

    [Fact]
    public async Task Cancelled_workflows_cannot_return_output()
    {
        using var client = new ScriptedChatClient((_, _) => throw new InvalidOperationException("No model call expected."));
        var events = new List<WorkflowEvent>();
        var token = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WelcomeWorkflowDemo.RunAsync(cancellationToken: token, onEvent: events.Add));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ScheduleWorkflowDemo.RunAsync(client, "Foundry", cancellationToken: token, onEvent: events.Add));
        Assert.Empty(events);
    }

    [Fact]
    public async Task Schedule_workflow_preserves_question_grounding_and_one_model_call()
    {
        const string question = "Which Foundry sessions on day 2 fit my interests?";
        const string expected = "Grounded recommendation [#s13](Update_Conference_Prague_2026_Schedule.md#s13).";
        var calls = 0;
        var events = new List<WorkflowEvent>();
        using var client = new ScriptedChatClient((messages, options) =>
        {
            calls++;
            var request = messages.ToArray();
            Assert.Contains(request, m => m.Role == ChatRole.User &&
                m.Text.Contains(question, StringComparison.Ordinal));
            Assert.Contains("Answer only from the supplied conference schedule snapshot.", options?.Instructions);
            LocalDemoTests.AssertScheduleContext(request, options);
            Assert.Null(options?.ResponseFormat);
            return Text(expected);
        });

        var answer = await ScheduleWorkflowDemo.RunAsync(client, question, onEvent: events.Add);

        Assert.Equal(1, calls);
        Assert.Equal(expected, answer);
        Assert.Equal(["PrepareQuestion", "AnswerFromSchedule"],
            events.OfType<ExecutorCompletedEvent>().Select(e => e.ExecutorId));
        Assert.Equal(answer, Assert.Single(events.OfType<WorkflowOutputEvent>()).As<string>());
        Assert.DoesNotContain("## Day 1", answer);
    }

    [Fact]
    public async Task Schedule_workflow_preserves_no_evidence_answer()
    {
        using var client = LocalDemoTests.ScheduleClient();
        Assert.Contains("No matching session", await ScheduleWorkflowDemo.RunAsync(client, "unicorn astrophysics"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Invalid_schedule_workflow_requests_fail_before_inference(string question)
    {
        using var client = new ScriptedChatClient((_, _) => throw new InvalidOperationException("No model call expected."));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ScheduleWorkflowDemo.RunAsync(client, question));
        Assert.Contains("PrepareQuestion", error.Message);
    }

    [Fact]
    public async Task Oversized_schedule_workflow_request_fails_before_inference()
    {
        using var client = new ScriptedChatClient((_, _) => throw new InvalidOperationException("No model call expected."));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ScheduleWorkflowDemo.RunAsync(client, new string('x', 4001)));
        Assert.Contains("PrepareQuestion", error.Message);
    }

    [Fact]
    public async Task Schedule_workflow_surfaces_inference_failures_without_output()
    {
        using var client = new ScriptedChatClient((_, _) => throw new HttpRequestException("Inference failed."));
        var events = new List<WorkflowEvent>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ScheduleWorkflowDemo.RunAsync(client, "Foundry", onEvent: events.Add));
        Assert.Contains("AnswerFromSchedule", error.Message);
        Assert.Contains("Inference failed.", error.ToString());
        Assert.Empty(events.OfType<WorkflowOutputEvent>());
    }

    [Fact]
    public async Task Schedule_workflow_cancellation_during_inference_cannot_return_output()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new ScriptedChatClient((_, _) =>
        {
            cancellation.Cancel();
            return Text("Must not be returned.");
        });
        var events = new List<WorkflowEvent>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ScheduleWorkflowDemo.RunAsync(client, "Foundry", cancellationToken: cancellation.Token, onEvent: events.Add));
        Assert.Empty(events.OfType<WorkflowOutputEvent>());
    }

    private static ChatResponse Text(string value) => new(new ChatMessage(ChatRole.Assistant, value));
}
