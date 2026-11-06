using AgenticDesignPatterns.Shared;
using Extension02.DecisionModels;
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
                  4. Planning and goal monitoring: dotnet run --project ConferenceDemos -- plan [--rounds 1-5]
                  5.1 Decision Model - compare sample: dotnet run --project ConferenceDemos -- decisions
                  5.2 Decision Model - compare recorded OpenAI: dotnet run --project ConferenceDemos -- decisions <openai-capture.json>
                  5.3 Decision Model - record OpenAI: dotnet run --project ConferenceDemos -- decisions-record-openai <new-capture.json>
                  6. Harness: dotnet run --project ConferenceDemos -- speaker-page [--open]
                  6.1 Harness + web search: dotnet run --project ConferenceDemos -- speaker-page --web [--open]
                See demo-flows.md for diagrams for demos 2, 4, 6, and 6.1.
                See README.md for credentials, recording, and project-local hosted deployment.
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
            if (command is "decisions" or "decisions-record-openai")
            {
                if (options.Length > 1 || (command == "decisions-record-openai" && options.Length != 1) ||
                    (options.Length == 1 && string.IsNullOrWhiteSpace(options[0])))
                {
                    throw new ArgumentException("decisions accepts an optional JSON path; decisions-record-openai requires one new capture path.");
                }
                var state = DecisionDemo.State();
                var demo = command == "decisions-record-openai"
                    ? "5.3 Decision Model - record OpenAI"
                    : options.Length == 0
                        ? "5.1 Decision Model - compare sample"
                        : "5.2 Decision Model - compare recorded OpenAI";
                Console.WriteLine($"{demo}\nShared session and attendee interest:\n{state}");
                var capture = command == "decisions"
                    ? DecisionDemo.ReadCapture(options.SingleOrDefault() ?? DecisionDemo.SamplePath, state) : null;
                if (capture?.Provider == DecisionDemo.SampleProvider)
                {
                    Console.WriteLine("\nUsing bundled ILLUSTRATIVE SAMPLE, not a recorded OpenAI response.");
                }
                var settings = FoundryChatClientFactory.LoadSettings();
                using var decisions = DecisionConfiguration.CreateClient(settings, command == "decisions" ? "microsoft" : "openai");
                var result = await DecisionDemo.EvaluateAsync(decisions, state, cancellation.Token);
                if (command == "decisions-record-openai")
                {
                    var recorded = DecisionDemo.Capture(result, state, settings.ResolveModelDeployment("openaiDecision"));
                    DecisionDemo.ValidateCapture(recorded, state);
                    var path = Path.GetFullPath(options[0]);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                    await JsonSerializer.SerializeAsync(file, recorded, DecisionDemo.JsonOptions, cancellation.Token);
                    Console.WriteLine("Recorded a live Azure OpenAI Decisions result for all three tasks. Review it before rehearsal.");
                }
                else
                {
                    Console.WriteLine("\nMicrosoft-Decision-1 - LIVE (Choice / Noul / Score):\n" +
                        JsonSerializer.Serialize(result, DecisionDemo.JsonOptions));
                    var label = capture!.Provider == DecisionDemo.SampleProvider
                        ? "ILLUSTRATIVE SAMPLE - not a model response"
                        : $"Azure OpenAI Decisions - CAPTURED ({capture.CapturedAt:O}, model {capture.ModelDeployment})";
                    Console.WriteLine($"\n{label}:\n" +
                        JsonSerializer.Serialize(capture.Results, DecisionDemo.JsonOptions));
                    Console.WriteLine("\nAPI/output illustration only. Scores are not calibrated certainty or an accuracy benchmark.");
                }
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
        for (var i = 0; i < options.Length; i++)
        {
            switch (options[i])
            {
                case "--rounds" when i + 1 < options.Length && int.TryParse(options[i + 1], out var rounds) && rounds is >= 1 and <= 5:
                    request = request with { MaxIterations = rounds };
                    i++;
                    break;
                default:
                    throw new ArgumentException("plan accepts only --rounds 1-5.");
            }
        }

        var schedule = new ScheduleCorpus();
        Console.WriteLine("4. Planning and goal monitoring\n");

        Console.WriteLine($"\nGoal: plan {request.Attendee}'s day {request.Day} at Update Conference Prague 2026.\nInterests: {request.Interests}\n");
        using var client = FoundryChatClientFactory.CreateChatClient(FoundryChatClientFactory.LoadSettings());
        var result = await AgendaPlanningDemo.RunAsync(client, request, cancellationToken, evt =>
        {
            switch (evt)
            {
                case AgendaProgressEvent progress:
                    Console.WriteLine(progress.Message);
                    break;
                case ExecutorInvokedEvent invoked when invoked.ExecutorId is AgendaPlanningDemo.ConstraintMonitorId or AgendaPlanningDemo.FitCriticId:
                    Console.WriteLine($"  -> {invoked.ExecutorId} (parallel)");
                    break;
            }
        });

        Console.WriteLine($"\n=== {(result.Achieved ? "PUBLISHED" : "ESCALATED")} ===\n{result.Outcome}\n" +
            $"{AgendaPlanningDemo.FormatAgenda(result.Agenda, schedule)}\n\n{result.Agenda.Summary}\n\nWhy:");
        foreach (var session in result.Agenda.Sessions)
        {
            Console.WriteLine($"  #{session.Id}: {session.Why}");
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
