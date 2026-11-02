using System.Collections.Concurrent;
using AgenticDesignPatterns.TestSupport;
using Chapter11.GoalSettingMonitoring;
using Chapter11.GoalSettingMonitoring.Hosted;
using Chapter11.GoalSettingMonitoring.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.GoalSettingMonitoring.Tests;

public sealed class GoalSettingWorkflowTests
{
    [Fact]
    public async Task Stops_after_judge_confirms_all_goals()
    {
        using var model = new ScriptedModel(["print('first')", "reviewed", "True"]);

        var result = await GoalSettingWorkflow.RunAsync(
            model,
            GoalSettingRequest.BinaryGapSample with { MaxIterations = 5 });

        Assert.True(result.GoalsMet);
        Assert.Single(result.Iterations);
        Assert.Equal(3, model.Requests.Count);
        Assert.Equal("CodeGenerator", model.Requests[0].Agent);
        Assert.Equal("GoalReviewer", model.Requests[1].Agent);
        Assert.Equal("GoalJudge", model.Requests[2].Agent);
    }

    [Fact]
    public async Task Revises_again_with_previous_code_and_feedback_until_budget_is_exhausted()
    {
        using var model = new ScriptedModel(
            ["draft one", "add edge-case handling", "False", "draft two", "reviewed again", "False"]);

        var result = await GoalSettingWorkflow.RunAsync(
            model,
            GoalSettingRequest.BinaryGapSample with { MaxIterations = 2 });

        Assert.False(result.GoalsMet);
        Assert.Equal(2, result.Iterations.Count);
        Assert.Equal(6, model.Requests.Count);
        Assert.Contains("draft one", model.Requests[3].Input);
        Assert.Contains("add edge-case handling", model.Requests[3].Input);
        Assert.Contains("not met within 2 iterations", result.Format());
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("valid use case", 0)]
    [InlineData("valid use case", 6)]
    public async Task Invalid_request_is_rejected_before_model_calls(string useCase, int iterations)
    {
        using var model = new ScriptedModel([]);
        var request = new GoalSettingRequest(useCase, GoalSettingRequest.DefaultGoals, iterations);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            GoalSettingWorkflow.RunAsync(model, request));

        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task Rejects_goal_judge_output_other_than_true_or_false()
    {
        using var model = new ScriptedModel(["draft", "review", "True because it looks good"]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GoalSettingWorkflow.RunAsync(model, GoalSettingRequest.BinaryGapSample));

        Assert.Contains("exactly True or False", exception.Message);
    }

    [Fact]
    public async Task Console_demo_saves_final_code_with_a_sanitized_unique_filename()
    {
        using var model = new ScriptedModel(["print('artifact')", "reviewed", "True"]);
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"chapter11-test-{Guid.NewGuid():N}");

        try
        {
            var path = await GoalSettingDemo.RunAsync(
                model,
                GoalSettingRequest.BinaryGapSample with
                {
                    UseCase = "Write code: binary gap?"
                },
                outputDirectory,
                writeOutput: _ => { });

            Assert.StartsWith(Path.Combine(outputDirectory, "generated-code"), path);
            Assert.EndsWith(".py", path);
            var contents = await File.ReadAllTextAsync(path);
            Assert.StartsWith(
                "# Generated Python program for the requested use case." + Environment.NewLine +
                "# Caller use case:" + Environment.NewLine +
                "# Write code: binary gap?",
                contents);
            Assert.Contains("print('artifact')", contents);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Console_demo_keeps_caller_encoding_text_out_of_python_encoding_cookie_lines()
    {
        using var model = new ScriptedModel(["print('café')", "reviewed", "True"]);
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"chapter11-encoding-test-{Guid.NewGuid():N}");

        try
        {
            var path = await GoalSettingDemo.RunAsync(
                model,
                GoalSettingRequest.BinaryGapSample with
                {
                    UseCase = "Parse CSV input with encoding: ascii"
                },
                outputDirectory,
                writeOutput: _ => { });

            var contents = await File.ReadAllTextAsync(path);
            var lines = contents.Split(["\r\n", "\n"], StringSplitOptions.None);
            Assert.StartsWith("# Generated Python program", lines[0]);
            Assert.StartsWith("# Caller use case:", lines[1]);
            Assert.Contains("# Parse CSV input with encoding: ascii", lines[2]);
            Assert.Contains("café", contents);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Responses_protocol_processes_the_callers_structured_request_without_reusing_state()
    {
        using var model = new ScriptedModel(["print('caller request')", "All stated goals are met.", "True"]);
        await using var host = await HostedResponses.StartAsync(new GoalSettingAgent(model));

        using var body = await host.InvokeAsync(
            """
            {
              "useCase": "Generate a caller-provided utility.",
              "goals": ["Return a Python function"],
              "maxIterations": 1
            }
            """,
            "chapter11-goal-setting-monitoring");
        var responseText = HostedResponses.OutputText(body.RootElement);

        Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
        Assert.Contains("Goal status: met", responseText);
        Assert.Contains("print('caller request')", responseText);
        Assert.Contains("Generate a caller-provided utility.", model.Requests[0].Input);
        Assert.Contains("Return a Python function", model.Requests[0].Input);
        Assert.Equal(3, model.Requests.Count);
    }

    private sealed record ModelRequest(string Agent, string Input);

    // Replays queued responses and records which agent (by instructions) made each call.
    private sealed class ScriptedModel : DelegatingChatClient
    {
        private readonly ConcurrentQueue<ModelRequest> _requests = new();

        public ScriptedModel(IEnumerable<string> responses) : this(new Queue<string>(responses)) { }

        private ScriptedModel(Queue<string> responses) : base(new ScriptedChatClient((messages, options) =>
            new ChatResponse(new ChatMessage(ChatRole.Assistant, responses.TryDequeue(out var response)
                ? response
                : throw new InvalidOperationException("No scripted response remains.")))))
        {
        }

        public IReadOnlyList<ModelRequest> Requests => _requests.ToArray();

        public override Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var instructions = options?.Instructions ?? string.Empty;
            var agent = instructions.Contains("Python code generator", StringComparison.Ordinal) ? "CodeGenerator"
                : instructions.Contains("independent Python code reviewer", StringComparison.Ordinal) ? "GoalReviewer"
                : instructions.Contains("strict goal-completion judge", StringComparison.Ordinal) ? "GoalJudge"
                : "Unknown";
            _requests.Enqueue(new ModelRequest(
                agent, messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty));
            return base.GetResponseAsync(messages, options, cancellationToken);
        }
    }
}
