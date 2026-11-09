using Xunit;
using AgenticDesignPatterns.Shared;
using HumanInTheLoop;
using Microsoft.Extensions.AI;

namespace AgenticDesignPatterns.Foundry.SmokeTests;

[Trait("Category", "LiveFoundry")]
public sealed class FoundrySmokeTests
{
    [Fact(Timeout = 240000)]
    public async Task Chapter13_customer_support_escalation_runs_with_live_foundry_configuration()
    {
        var settings = FoundryChatClientFactory.LoadSettings();
        var deploymentAlias = Environment.GetEnvironmentVariable("FOUNDRY_TEST_DEPLOYMENT_ALIAS");
        using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        var agent = CustomerSupportAgent.Create(
            chatClient,
            new SupportCustomer
            {
                Name = "Sam",
                Tier = "premium",
                RecentPurchases = ["Wireless headphones"],
                SupportHistory = ["The device disconnected during a previous support session."]
            });
        var session = await agent.CreateSessionAsync();

        var pending = await agent.RunAsync(
            "This is a complex, persistent headphone issue. Use escalate_to_human to transfer it to a specialist now.",
            session);
        var approval = Assert.Single(pending.Messages
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>());

        Assert.Equal("escalate_to_human", Assert.IsType<FunctionCallContent>(approval.ToolCall).Name);

        var completed = await agent.RunAsync(
            new ChatMessage(ChatRole.User, [approval.CreateResponse(approved: true)]),
            session);

        Assert.Contains("Escalated", completed.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter19_evaluation_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter19EvaluationAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("Response accuracy: 0", result.StandardOutput);
        Assert.Contains("--- MAF response latency and provider token usage ---", result.StandardOutput);
        Assert.Contains("--- Legal survey judge: Biased ---", result.StandardOutput);
        Assert.Contains("\"overall_score\"", result.StandardOutput);
        Assert.Contains("--- MEAI semantic equivalence: notebook paraphrase ---", result.StandardOutput);
        Assert.Contains("Legal Survey Quality:", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter01_specs_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter01SpecsAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Final output ---", result.StandardOutput);
        Assert.True(result.StandardOutput.Contains("\"cpu\"", StringComparison.OrdinalIgnoreCase), result.CombinedOutput);
        Assert.True(result.StandardOutput.Contains("\"memory\"", StringComparison.OrdinalIgnoreCase), result.CombinedOutput);
        Assert.True(result.StandardOutput.Contains("\"storage\"", StringComparison.OrdinalIgnoreCase), result.CombinedOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter02_travel_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter02TravelAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Coordinator router decision ---", result.StandardOutput);
        Assert.Contains("--- Booker specialist output ---", result.StandardOutput);
        Assert.Contains("--- Info specialist output ---", result.StandardOutput);
        Assert.Contains("--- Clarification fallback ---", result.StandardOutput);
        Assert.Contains("--- Final routed response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter03_topic_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter03TopicAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Topic summary (parallel branch) ---", result.StandardOutput);
        Assert.Contains("--- Related questions (parallel branch) ---", result.StandardOutput);
        Assert.Contains("--- Key terms (parallel branch) ---", result.StandardOutput);
        Assert.Contains("--- Final synthesized response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter04_fact_check_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter04FactCheckAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Generated draft (round 1 of 3) ---", result.StandardOutput);
        Assert.Contains("--- Independent verification ---", result.StandardOutput);
        Assert.Contains("--- Final result ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter05_search_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter05SearchAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.True(
            result.StandardOutput.Split("Tool called: search_information", StringSplitOptions.None).Length >= 4,
            result.CombinedOutput);
        Assert.Contains("--- Final agent response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter06_plan_and_write_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter06PlanWriteAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Plan ---", result.StandardOutput);
        Assert.Contains("--- Summary ---", result.StandardOutput);
        Assert.Contains("### Plan", result.StandardOutput);
        Assert.Contains("### Summary", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter07_crew_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter07CrewAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Crew research task ---", result.StandardOutput);
        Assert.Contains("--- Crew writing task ---", result.StandardOutput);
        Assert.Contains("--- Final output ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter08_session_memory_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter08SessionMemoryAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Explicit session-state update ---", result.StandardOutput);
        Assert.Contains("--- Recall from the same session ---", result.StandardOutput);
        Assert.Contains("travel preference: window seats when I travel", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--- Serialize and restore the session ---", result.StandardOutput);
        Assert.Contains("Stored output key: last_response", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter09_learning_adaptation_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter09LearningAdaptationAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Baseline evaluation ---", result.StandardOutput);
        Assert.True(
            result.StandardOutput.Contains("Candidate score:", StringComparison.Ordinal) ||
            result.StandardOutput.Contains(
                "The starting prompt already passes the benchmark; no update is recommended.",
                StringComparison.Ordinal),
            result.CombinedOutput);
        Assert.Contains("--- Adaptation result ---", result.StandardOutput);
        Assert.Contains("Best-scoring prompt:", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter21_exploration_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter21ExplorationAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Final exploration result ---", result.StandardOutput);
        Assert.Contains("Final reviewer assessments", result.StandardOutput);
        Assert.Contains("unverified proposal", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter17_reasoning_demos_run_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter17ReasoningAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("Reasoning prompt with a concise rationale (cot)", result.StandardOutput);
        Assert.Contains("Bounded self-correction (self-correction)", result.StandardOutput);
        Assert.Contains("ReAct with a deterministic factorial tool (react)", result.StandardOutput);
        Assert.Contains("Bounded query planning, reflection, and synthesis (deep-search)", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter14_rag_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter14RagAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Retrieved context ---", result.StandardOutput);
        Assert.Contains("SOTU-2022-BREYER", result.StandardOutput);
        Assert.Contains("--- Final grounded response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter11_goal_setting_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter11GoalSettingMonitoringAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Generated code (iteration 1) ---", result.StandardOutput);
        Assert.Contains("--- Goal review (iteration 1) ---", result.StandardOutput);
        Assert.Contains("--- Goal decision (iteration 1) ---", result.StandardOutput);
        Assert.Contains("--- Final Python code ---", result.StandardOutput);
        Assert.Contains("Saved generated code to:", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter16_complexity_routing_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter16ComplexityAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Complexity router decision ---", result.StandardOutput);
        Assert.Contains("Route: Simple", result.StandardOutput);
        Assert.Contains("Route: Reasoning", result.StandardOutput);
        Assert.Contains("--- Final optimized response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter12_recovery_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter12RecoveryAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Checkpoint: primary lookup succeeded on attempt 1 ---", result.StandardOutput);
        Assert.Contains("--- Checkpoint: primary lookup succeeded on attempt 2 ---", result.StandardOutput);
        Assert.Contains("--- General-area fallback ---", result.StandardOutput);
        Assert.Contains("--- Final recovered response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter18_policy_guardrail_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter18PolicyAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Structured policy result ---", result.StandardOutput);
        Assert.Contains("Decision: COMPLIANT", result.StandardOutput);
        Assert.Contains("--- Guarded assistant response ---", result.StandardOutput);
    }
}
