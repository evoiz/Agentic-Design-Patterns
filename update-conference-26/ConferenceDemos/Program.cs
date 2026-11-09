using AgenticDesignPatterns.Shared;
using DecisionModels;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace ConferenceDemos;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args is ["--help"])
        {
            Console.WriteLine("""
                Conference demos (run from update-conference-26):
                  Presentation order:
                  1. Prague weather: dotnet run --project ConferenceDemos -- agent
                  2. Simple MAF workflow: dotnet run --project ConferenceDemos -- workflow [attendee name]
                  3. Schedule RAG: dotnet run --project ConferenceDemos -- rag [question]
                  4. Planning and goal monitoring: dotnet run --project ConferenceDemos -- plan [--rounds 1-5] [--verbose]
                  5.1 Decision Model - Microsoft live: dotnet run --project ConferenceDemos -- decisions
                  5.2 Decision Model - live comparison: dotnet run --project ConferenceDemos -- decisions --compare-openai
                  6. Harness: dotnet run --project ConferenceDemos -- speaker-page [--open]
                  6.1 Harness + web search: dotnet run --project ConferenceDemos -- speaker-page --web [--open]
                See demo-flows.md for diagrams for demos 2, 4, 6, and 6.1.
                See README.md for credentials and project-local hosted deployment.
                """);
            return 0;
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        try
        {
            var command = args[0];
            var options = args[1..];
            if (command == "workflow")
            {
                if (options.Any(o => o.StartsWith("--", StringComparison.Ordinal)))
                {
                    throw new ArgumentException("workflow accepts an optional attendee name, not options.");
                }
                Console.WriteLine("2. Simple MAF workflow (no model calls)\nGetAttendeeProfile -> RecommendSession -> CreateRecommendation\n");
                var attendee = options.Length == 0 ? "Alex" : string.Join(" ", options);
                Console.WriteLine(await WelcomeWorkflowDemo.RunAsync(attendee, cancellation.Token, PrintWorkflowEvent));
                Pause();
                return 0;
            }
            if (command == "agent")
            {
                if (options.Length != 0)
                {
                    throw new ArgumentException("agent takes no arguments; it runs the welcome and Prague weather questions.");
                }
                Console.WriteLine("1. Prague weather: agent + tool\nWeather is fixed demo data, not a live forecast.\n");
                using var agentClient = FoundryChatClientFactory.CreateChatClient(FoundryChatClientFactory.LoadSettings());
                await AgentDemo.RunAsync(agentClient, Console.Out, cancellation.Token);
                Pause();
                return 0;
            }
            if (command == "plan")
            {
                return await RunPlanAsync(options, cancellation.Token);
            }
            if (command == "speaker-page")
            {
                return await RunSpeakerPageAsync(options, cancellation.Token);
            }
            if (command == "decisions")
            {
                if (options.Length != 0 && options is not ["--compare-openai"])
                {
                    throw new ArgumentException("decisions accepts only the optional --compare-openai flag. All results are live API responses.");
                }
                var state = DecisionDemo.State();
                var compare = options.Length != 0;
                var demo = compare ? "5.2 Decision Model - live comparison" : "5.1 Decision Model - Microsoft live";
                Console.WriteLine($"{demo}\nShared evidence: complete conference schedule (both days).\n" +
                    $"Attendee goal: {DecisionDemo.AttendeeGoal}\n" +
                    "Binary API illustration: the snapshot lists Cédric on Day 1, not Day 2; production fact lookup should be deterministic.\n" +
                    "Choice and score are judgments about published session coverage, not a guaranteed winner or confidence benchmark.");
                var settings = FoundryChatClientFactory.LoadSettings();
                using var decisions = DecisionConfiguration.CreateClient(settings, "microsoft");
                using var openai = compare ? DecisionConfiguration.CreateClient(settings, "openai") : null;
                await DecisionDemo.EvaluateAsync(decisions, state, cancellation.Token, Console.WriteLine, openai);
                Console.WriteLine("\nLive API/output illustration only. Scores are not calibrated certainty or an accuracy benchmark.");
                Pause();
                return 0;
            }

            if (command != "rag")
            {
                throw new ArgumentException($"Unknown command '{command}'. Use --help.");
            }
            if (options.Any(o => o.StartsWith("--", StringComparison.Ordinal)))
            {
                throw new ArgumentException("Unsupported option. Use --help for the supported command shape.");
            }
            var question = options.Length == 0 ? ScheduleDemo.DefaultQuestion : string.Join(" ", options);
            Console.WriteLine($"User question: {question}\n");
            using IChatClient client = FoundryChatClientFactory.CreateChatClient(FoundryChatClientFactory.LoadSettings());
            Console.WriteLine("3. Schedule RAG (TextSearchProvider)\nPrepareQuestion -> AnswerFromSchedule\n");
            Console.WriteLine(await ScheduleWorkflowDemo.RunAsync(
                client, question, cancellation.Token, PrintWorkflowEvent));
            Pause();
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or
            JsonException or HttpRequestException or OperationCanceledException or ElBruno.AI.Decisions.DecisionException or
            System.ClientModel.ClientResultException or AggregateException)
        {
            Console.Error.WriteLine($"Demo failed: {(ex is AggregateException agg ? agg.InnerExceptions[^1].Message : ex.Message)}");
            return 1;
        }
    }

    private static async Task<int> RunPlanAsync(string[] options, CancellationToken cancellationToken)
    {
        var request = AgendaPlanningDemo.DefaultRequest;
        var verbose = false;
        for (var i = 0; i < options.Length; i++)
        {
            switch (options[i])
            {
                case "--verbose":
                    verbose = true;
                    break;
                case "--rounds" when i + 1 < options.Length && int.TryParse(options[i + 1], out var rounds) && rounds is >= 1 and <= 5:
                    request = request with { MaxIterations = rounds };
                    i++;
                    break;
                default:
                    throw new ArgumentException("plan accepts --rounds 1-5 and --verbose.");
            }
        }

        var schedule = new ScheduleCorpus();
        Console.WriteLine("4. Planning and goal monitoring\n");

        Console.WriteLine(AgendaPlanningDemo.WrapConsoleText(
            $"Give me {request.SessionCount} interesting sessions on conference day {request.Day} for {request.Attendee}.", "Request: ", "         "));
        Console.WriteLine(AgendaPlanningDemo.WrapConsoleText(request.Interests, "Interests: ", "           ") + "\n");
        using var client = FoundryChatClientFactory.CreateChatClient(FoundryChatClientFactory.LoadSettings());
        var result = await AgendaPlanningDemo.RunAsync(client, request, cancellationToken, evt =>
        {
            switch (evt)
            {
                case AgendaProgressEvent progress:
                    Console.WriteLine(progress.Message);
                    break;
            }
        }, verbose);

        Console.WriteLine($"\n=== {(result.Achieved ? "PUBLISHED" : "ESCALATED")} ===\n" +
            AgendaPlanningDemo.WrapConsoleText(result.Outcome) + "\n" +
            AgendaPlanningDemo.WrapConsoleText(result.Agenda.Summary));
        if (verbose)
        {
            Console.WriteLine("\nFINAL AGENDA\n" + AgendaPlanningDemo.FormatAgenda(result.Agenda, schedule) + "\n\nWHY");
            foreach (var session in result.Agenda.Sessions)
            {
                Console.WriteLine(AgendaPlanningDemo.WrapConsoleText(session.Why, $"  #{session.Id}: ", "       "));
            }
        }
        Pause();
        return result.Achieved ? 0 : 2;
    }

    private static async Task<int> RunSpeakerPageAsync(string[] options, CancellationToken cancellationToken)
    {
        if (options.Except(["--web", "--open"]).Any())
        {
            throw new ArgumentException("speaker-page accepts only --web and --open.");
        }
        var web = options.Contains("--web");
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var profile = SpeakerPageDemo.LoadProfile();
        var output = SpeakerPageDemo.DefaultOutputDirectory;
        var demo = web ? "6.1 Harness + web search" : "6. Harness";
        Console.WriteLine($"{demo}: build {profile.Name}'s speaker page\n\nHarness tool calls:");

        using var client = SpeakerPageDemo.CreateClient(FoundryChatClientFactory.LoadSettings());
        var checks = await SpeakerPageDemo.RunAsync(client, profile, output, web, Console.WriteLine, cancellationToken);

        var passed = checks.All(c => c.Passed);
        var pagePath = Path.Combine(output, SpeakerPageDemo.PageFile);
        Console.WriteLine($"\n{(passed ? "✅ Speaker page ready" : "❌ Speaker page incomplete")}");
        foreach (var check in checks)
        {
            Console.WriteLine($"   {(check.Passed ? "✓" : "✗")} {check.Check,-14} {check.Evidence}");
        }
        Console.WriteLine($"   → {new Uri(pagePath)}");
        if (options.Contains("--open") && File.Exists(pagePath))
        {
            // Demo only: model-generated HTML is not sanitized and opens even if checks fail.
            // Do not use this preview behavior in production; file-tool sandboxing does not isolate the browser.
            SpeakerPageDemo.Open(pagePath);
        }
        Pause();
        return passed ? 0 : 1;
    }

    private static void Pause()
    {
        if (!Console.IsInputRedirected)
        {
            Console.ReadKey();
        }
    }

    private static void PrintWorkflowEvent(WorkflowEvent evt)
    {
        switch (evt)
        {
            case ExecutorCompletedEvent completed:
                Console.WriteLine($"ExecutorCompletedEvent: {completed.ExecutorId}");
                break;
            case WorkflowOutputEvent:
                Console.WriteLine("WorkflowOutputEvent: final answer\n");
                break;
        }
    }
}
