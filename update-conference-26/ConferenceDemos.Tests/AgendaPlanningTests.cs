using AgenticDesignPatterns.TestSupport;
using ConferenceDemos;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class AgendaPlanningTests
{
    private static readonly int[] Valid = [1, 6, 13, 22];
    private static readonly int[] Clashing = [1, 2, 10, 13];

    [Fact]
    public void Constraint_monitor_accepts_a_valid_agenda_and_explains_each_violation()
    {
        var schedule = new ScheduleCorpus();
        var request = AgendaPlanningDemo.DefaultRequest;
        Assert.All(AgendaPlanningDemo.CheckConstraints(request, Valid, schedule), g => Assert.True(g.Met, g.Goal));

        var bad = AgendaPlanningDemo.CheckConstraints(request, [1, 2, 10, 26, 999, 1], schedule).ToDictionary(g => g.Goal);
        Assert.False(bad["Grounded"].Met);
        Assert.Contains("#999", bad["Grounded"].Evidence);
        Assert.False(bad["Day 1"].Met);
        Assert.False(bad["Size"].Met);
        Assert.False(bad["No overlaps"].Met);
        Assert.Contains("#1/#2", bad["No overlaps"].Evidence);
        Assert.False(bad["Must attend"].Met);
        Assert.False(bad["Keep free"].Met);
        Assert.Contains("#10", bad["Keep free"].Evidence);
    }

    [Fact]
    public void Graph_is_built_without_a_model_and_shows_fan_out_fan_in_and_routing()
    {
        var mermaid = WorkflowVisualizer.ToMermaidString(AgendaPlanningDemo.BuildWorkflow(null, new ScheduleCorpus()));
        foreach (var id in new[] { "PlanTasks", "BuildAgenda", "CheckConstraints", "CritiqueFit", "MonitorGoals", "PublishAgenda", "Escalate" })
        {
            Assert.Contains(id, mermaid);
        }
        Assert.Contains("fan-in", mermaid);
        Assert.Contains("goals unmet: revise", mermaid);
        Assert.Contains("all goals met", mermaid);
        Assert.Contains("budget exhausted", mermaid);
    }

    [Fact]
    public async Task Failed_goals_loop_back_to_the_planner_with_feedback_then_publish()
    {
        var plannerCalls = 0;
        string? secondPlannerInput = null;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            if (IsPlanner(options)) { return LocalDemoTests.Json(TaskPlan()); }
            if (IsCritic(options))
            {
                return LocalDemoTests.Json(new FitCritique(5, "Relevant choices."));
            }
            if (++plannerCalls == 2)
            {
                secondPlannerInput = string.Join("\n", messages.Select(m => m.Text));
            }
            return LocalDemoTests.Json(Agenda(plannerCalls == 1 ? Clashing : Valid));
        });
        var events = new List<string>();

        var result = await AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest, onEvent: e =>
        {
            if (e is AgendaProgressEvent p) { events.Add(p.Message); }
        });

        Assert.True(result.Achieved);
        Assert.Equal(2, plannerCalls);
        Assert.Equal(2, result.History.Count);
        Assert.Contains("goals unmet", result.History[0].Route);
        Assert.Contains("PublishAgenda", result.History[1].Route);
        Assert.Equal(Valid, result.Agenda.Sessions.Select(s => s.Id));
        Assert.Contains("#1/#2 both at 09:15", secondPlannerInput);
        Assert.Contains("Keep free", secondPlannerInput);
        Assert.Contains("Published after 2 round(s)", result.Outcome);
        Assert.Equal(2, events.Count(e => e.Contains("Goal monitor", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Critic_receives_all_goals_and_its_repair_advice_reaches_replanning()
    {
        var round = 0;
        const string advice = "Keep free: remove #10 at 12:30 and choose #6 for .NET; use #1 and #22 for AI.";
        using var client = new ScriptedChatClient((messages, options) =>
        {
            var input = string.Join("\n", messages.Select(m => m.Text));
            if (IsPlanner(options))
            {
                if (++round == 2) { Assert.Contains(advice, input); }
                return LocalDemoTests.Json(TaskPlan());
            }
            if (IsCritic(options))
            {
                foreach (var goal in AgendaPlanningDemo.DescribeGoals(AgendaPlanningDemo.DefaultRequest))
                {
                    Assert.Contains(goal, input);
                }
                Assert.Contains("Schedule", input);
                Assert.Contains("HardGoalChecks", input);
                Assert.Contains("12:30", input);
                if (round == 1)
                {
                    Assert.Contains("booked #10", input);
                    Assert.Contains("\"Met\":false", input);
                }
                Assert.DoesNotContain("Ignore timing rules", options?.Instructions);
                return LocalDemoTests.Json(round == 1 ? new FitCritique(3, advice)
                    : new FitCritique(5, "All goals pass and the sessions match the interests."));
            }
            if (round == 2) { Assert.Contains(advice, input); }
            return LocalDemoTests.Json(Agenda(round == 1 ? [5, 10, 13, 17] : Valid));
        });
        var result = await AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest);
        Assert.True(result.Achieved);
        Assert.False(result.History[0].Goals.Single(g => g.Goal == "Fit").Met);
        Assert.Contains(advice, result.History[0].Goals.Single(g => g.Goal == "Fit").Evidence);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Empty_critic_feedback_is_rejected_even_with_a_perfect_score(int score)
    {
        using var client = new ScriptedChatClient((_, options) => IsPlanner(options)
            ? LocalDemoTests.Json(TaskPlan())
            : IsCritic(options) ? LocalDemoTests.Json(new FitCritique(score, " "))
                : LocalDemoTests.Json(Agenda(Valid)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest));
    }

    [Fact]
    public async Task Low_critic_score_forces_a_revision_even_when_constraints_pass()
    {
        var criticCalls = 0;
        using var client = new ScriptedChatClient((_, options) => IsCritic(options)
            ? LocalDemoTests.Json(++criticCalls == 1 ? new FitCritique(2, "Swap #17 for #21.") : new FitCritique(4, "Good."))
            : IsPlanner(options) ? LocalDemoTests.Json(TaskPlan()) : LocalDemoTests.Json(Agenda(Valid)));

        var result = await AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest);

        Assert.True(result.Achieved);
        Assert.Equal(2, result.History.Count);
        Assert.False(result.History[0].Goals.Single(g => g.Goal == "Fit").Met);
    }

    [Fact]
    public async Task Budget_exhaustion_routes_to_escalation_with_the_unmet_goals()
    {
        var plannerCalls = 0;
        using var client = new ScriptedChatClient((_, options) =>
        {
            if (IsPlanner(options)) { return LocalDemoTests.Json(TaskPlan()); }
            if (IsCritic(options)) { return LocalDemoTests.Json(new FitCritique(5, "Relevant choices.")); }
            plannerCalls++;
            return LocalDemoTests.Json(Agenda(Clashing));
        });

        var result = await AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest with { MaxIterations = 2 });

        Assert.False(result.Achieved);
        Assert.Equal(2, plannerCalls);
        Assert.Equal(2, result.History.Count);
        Assert.Contains("Escalated", result.Outcome);
        Assert.Contains("No overlaps", result.Outcome);
    }

    [Fact]
    public async Task Invalid_critic_output_and_requests_fail_explicitly()
    {
        using var client = new ScriptedChatClient((_, options) => IsCritic(options)
            ? LocalDemoTests.Json(new FitCritique(9, "Too good."))
            : IsPlanner(options) ? LocalDemoTests.Json(TaskPlan()) : LocalDemoTests.Json(Agenda(Valid)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest with { MaxIterations = 9 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest with { KeepFree = ["noon"] }));
    }

    [Fact]
    public async Task Initial_brief_omits_policies_and_builder_consumes_the_task_plan_with_real_times()
    {
        var calls = 0;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            calls++;
            var input = string.Join("\n", messages.Select(m => m.Text));
            if (IsCritic(options)) { return LocalDemoTests.Json(new FitCritique(5, "Relevant choices.")); }
            Assert.DoesNotContain("KeepFree", input);
            Assert.DoesNotContain("No overlaps", input);
            Assert.DoesNotContain("preparation", input);
            if (IsPlanner(options))
            {
                Assert.Contains("4 interesting sessions", input);
                return LocalDemoTests.Json(TaskPlan());
            }
            Assert.Contains("TaskPlan", input);
            Assert.Contains(TaskPlan().Tasks[0].Action, input);
            Assert.Contains("12:30", input);
            Assert.Contains("Start", input);
            Assert.Contains("End", input);
            Assert.DoesNotContain("Slots", input);
            return LocalDemoTests.Json(Agenda(Valid));
        });
        var result = await AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest);
        Assert.True(result.Achieved);
        Assert.Single(result.History);
        Assert.Equal(3, calls);
        Assert.Equal(TaskPlan().Tasks[0], result.History[0].TaskPlan.Tasks[0]);
    }

    [Theory]
    [InlineData(1, 2, 13, 22, "No overlaps")]
    [InlineData(1, 10, 13, 22, "Keep free")]
    public void Monitor_checks_the_two_initially_undisclosed_policies(int a, int b, int c, int d, string failedGoal)
    {
        var goals = AgendaPlanningDemo.CheckConstraints(AgendaPlanningDemo.DefaultRequest,
            [a, b, c, d], new ScheduleCorpus());
        Assert.False(goals.Single(g => g.Goal == failedGoal).Met);
        Assert.True(goals.Single(g => g.Goal == "Size").Met);
    }

    [Fact]
    public async Task Revealed_policies_persist_when_a_later_round_fails_only_fit()
    {
        var round = 0;
        var criticCalls = 0;
        var progress = new List<string>();
        using var client = new ScriptedChatClient((messages, options) =>
        {
            var input = string.Join("\n", messages.Select(m => m.Text));
            if (IsPlanner(options))
            {
                round++;
                if (round > 1)
                {
                    Assert.Contains("No overlaps", input);
                    Assert.Contains("12:30", input);
                    Assert.Contains("preparation", input);
                }
                return LocalDemoTests.Json(new AgendaTaskPlan([
                    new($"Plan round {round} using the current feedback.", "Updated selection strategy.", $"Plan round {round}"),
                    new("Build the agenda from the strategy.", "Four selected sessions.", "Build agenda")]));
            }
            if (IsCritic(options))
            {
                return LocalDemoTests.Json(++criticCalls == 2
                    ? new FitCritique(2, "Improve interest fit.") : new FitCritique(5, "Relevant choices."));
            }
            if (round > 1)
            {
                Assert.Contains("No overlaps", input);
                Assert.Contains("preparation", input);
            }
            Assert.DoesNotContain("PreviousAgenda", input);
            return LocalDemoTests.Json(Agenda(round == 1 ? Clashing : Valid));
        });
        var result = await AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest,
            onEvent: e => { if (e is AgendaProgressEvent p) { progress.Add(p.Message); } });
        Assert.True(result.Achieved);
        Assert.Equal(3, result.History.Count);
        Assert.False(result.History[0].Goals.Single(g => g.Goal == "Keep free").Met);
        Assert.False(result.History[0].Goals.Single(g => g.Goal == "No overlaps").Met);
        Assert.True(result.History[1].Goals.Single(g => g.Goal == "Keep free").Met);
        Assert.NotEqual(result.History[0].TaskPlan.Tasks[0], result.History[1].TaskPlan.Tasks[0]);
        Assert.Contains(progress, p => p.Contains("initially reviewer-only", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public async Task Invalid_task_plans_fail_before_building(int count)
    {
        var calls = 0;
        using var client = new ScriptedChatClient((_, _) =>
        {
            calls++;
            return LocalDemoTests.Json(new AgendaTaskPlan(
                Enumerable.Range(0, count).Select(_ => new AgendaTask("Select candidates.", "Candidates.", "Select candidates")).ToList()));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Empty_task_fields_and_missing_agenda_sessions_fail_explicitly()
    {
        using var invalidPlan = new ScriptedChatClient((_, _) => LocalDemoTests.Json(new AgendaTaskPlan([
            new(" ", "Candidates.", "Rank candidates"), new("Select sessions.", "", "Select sessions")])));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AgendaPlanningDemo.RunAsync(invalidPlan, AgendaPlanningDemo.DefaultRequest));
        using var invalidAgenda = new ScriptedChatClient((_, options) => IsPlanner(options)
            ? LocalDemoTests.Json(TaskPlan())
            : LocalDemoTests.Json(new { Summary = "No session list." }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AgendaPlanningDemo.RunAsync(invalidAgenda, AgendaPlanningDemo.DefaultRequest));
    }

    [Fact]
    public async Task One_round_can_escalate_or_publish_and_independent_runs_do_not_share_feedback()
    {
        var buildCalls = 0;
        using var client = new ScriptedChatClient((messages, options) =>
        {
            if (IsPlanner(options))
            {
                Assert.DoesNotContain("No overlaps", string.Join("\n", messages.Select(m => m.Text)));
                return LocalDemoTests.Json(TaskPlan());
            }
            if (IsCritic(options)) { return LocalDemoTests.Json(new FitCritique(5, "Relevant choices.")); }
            return LocalDemoTests.Json(Agenda(++buildCalls == 1 ? Clashing : Valid));
        });
        var request = AgendaPlanningDemo.DefaultRequest with { MaxIterations = 1 };
        var first = await AgendaPlanningDemo.RunAsync(client, request);
        var second = await AgendaPlanningDemo.RunAsync(client, request);
        Assert.False(first.Achieved);
        Assert.Contains("No overlaps", first.Outcome);
        Assert.True(second.Achieved);
    }

    [Fact]
    public void Compact_plan_shows_titles_and_verbose_plan_preserves_full_instructions()
    {
        var plan = TaskPlan();
        var compact = AgendaPlanningDemo.FormatTaskPlan(plan);
        Assert.Contains("1. Rank relevant sessions", compact);
        Assert.DoesNotContain(plan.Tasks[0].Action, compact);
        Assert.DoesNotContain(plan.Tasks[0].ExpectedOutcome, compact);
        var verbose = AgendaPlanningDemo.FormatTaskPlan(plan, verbose: true);
        Assert.Contains(plan.Tasks[0].Action, verbose);
        Assert.Contains(plan.Tasks[0].ExpectedOutcome, verbose);
    }

    [Fact]
    public void Console_wrapping_bounds_lines_without_dropping_text()
    {
        var text = string.Join(" ", Enumerable.Repeat("A readable critique with actionable session repairs.", 8));
        var formatted = AgendaPlanningDemo.WrapConsoleText(text, "  ", "  ", width: 48);
        Assert.All(formatted.Split('\n'), line => Assert.True(line.Length <= 48, line));
        Assert.Equal(text, string.Join(" ", formatted.Split('\n').Select(line => line.Trim())));
        var longWord = new string('x', 130);
        var split = AgendaPlanningDemo.WrapConsoleText(longWord, width: 48);
        Assert.All(split.Split('\n'), line => Assert.True(line.Length <= 48));
        Assert.Equal(longWord, string.Concat(split.Split('\n').Select(line => line.Trim())));
        Assert.Throws<ArgumentOutOfRangeException>(() => AgendaPlanningDemo.WrapConsoleText("text", width: 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Console_review_separates_critique_and_keeps_all_failure_evidence(bool verbose)
    {
        using var client = new ScriptedChatClient((_, options) => IsPlanner(options)
            ? LocalDemoTests.Json(TaskPlan())
            : IsCritic(options) ? LocalDemoTests.Json(new FitCritique(3, "Remove #10 to keep preparation free."))
                : LocalDemoTests.Json(Agenda(Clashing)));
        var events = new List<string>();
        await AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest with { MaxIterations = 1 },
            onEvent: e => { if (e is AgendaProgressEvent p) { events.Add(p.Message); } }, verbose: verbose);
        var output = string.Join("\n", events);
        Assert.Contains("PLAN", output);
        Assert.Contains("CRITIC [FAIL]", output);
        Assert.Contains("NEXT:", output);
        Assert.Contains("#1/#2 both at 09:15", output);
        Assert.Contains("booked #10", output);
        Assert.Contains("Remove #10 to keep preparation free.", output);
        Assert.All(output.Split('\n'), line => Assert.True(line.Length <= 96, line));
        if (verbose) { Assert.Contains(TaskPlan().Tasks[0].Action, output); }
        else { Assert.DoesNotContain(TaskPlan().Tasks[0].Action, output); }
    }

    [Theory]
    [InlineData("")]
    public async Task Invalid_plan_titles_fail_explicitly(string title)
    {
        using var client = new ScriptedChatClient((_, _) => LocalDemoTests.Json(new AgendaTaskPlan([
            new("Select sessions.", "Candidates.", title), new("Build agenda.", "Agenda.", "Build agenda")])));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest));
    }

    [Fact]
    public async Task Cancellation_and_model_errors_propagate()
    {
        using var client = new ScriptedChatClient((_, _) => throw new HttpRequestException("Model unavailable."));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest, new CancellationToken(true)));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest));
        Assert.Contains("PlanTasks", error.Message);
    }

    private static bool IsCritic(ChatOptions? options) =>
        options?.Instructions?.Contains("You critique a conference agenda", StringComparison.Ordinal) == true;

    private static bool IsPlanner(ChatOptions? options) =>
        options?.Instructions?.Contains("You plan the work", StringComparison.Ordinal) == true;

    private static AgendaTaskPlan TaskPlan() => new([
        new("Read the day schedule and rank relevant sessions.", "Ranked candidates.", "Rank relevant sessions"),
        new("Select the requested sessions and explain the choices.", "A grounded agenda.", "Select sessions")]);

    private static PlannedAgenda Agenda(int[] ids) =>
        new([.. ids.Select(id => new PlannedSession(id, $"Session {id} fits the interests."))], "A focused AI and .NET day.");
}
