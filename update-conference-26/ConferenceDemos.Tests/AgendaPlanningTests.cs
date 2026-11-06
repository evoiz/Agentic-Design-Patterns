using AgenticDesignPatterns.TestSupport;
using ConferenceDemos;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Xunit;

namespace ConferenceDemos.Tests;

public sealed class AgendaPlanningTests
{
    private static readonly int[] Valid = [1, 6, 13, 17, 22];
    private static readonly int[] Clashing = [1, 2, 10, 13, 22];

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
        foreach (var id in new[] { "ReadGoals", "PlanAgenda", "CheckConstraints", "CritiqueFit", "MonitorGoals", "PublishAgenda", "Escalate" })
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
            if (IsCritic(options))
            {
                return LocalDemoTests.Json(new FitCritique(5, ""));
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
    public async Task Low_critic_score_forces_a_revision_even_when_constraints_pass()
    {
        var criticCalls = 0;
        using var client = new ScriptedChatClient((_, options) => IsCritic(options)
            ? LocalDemoTests.Json(++criticCalls == 1 ? new FitCritique(2, "Swap #17 for #21.") : new FitCritique(4, "Good."))
            : LocalDemoTests.Json(Agenda(Valid)));

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
            if (IsCritic(options)) { return LocalDemoTests.Json(new FitCritique(5, "")); }
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
            : LocalDemoTests.Json(Agenda(Valid)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest with { MaxIterations = 9 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            AgendaPlanningDemo.RunAsync(client, AgendaPlanningDemo.DefaultRequest with { KeepFree = ["noon"] }));
    }

    private static bool IsCritic(ChatOptions? options) =>
        options?.Instructions?.Contains("You critique a conference agenda", StringComparison.Ordinal) == true;

    private static PlannedAgenda Agenda(int[] ids) =>
        new([.. ids.Select(id => new PlannedSession(id, $"Session {id} fits the interests."))], "A focused AI and .NET day.");
}
