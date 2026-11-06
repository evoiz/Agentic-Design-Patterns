using AgenticDesignPatterns.Shared;
using Extension02.DecisionModels;
using Microsoft.Agents.AI;
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
                  Getting started (in order):
                  dotnet run --project ConferenceDemos -- agent
                  dotnet run --project ConferenceDemos -- workflow [attendee name]
                  dotnet run --project ConferenceDemos -- rag [question]
                  Additional demos:
                  dotnet run --project ConferenceDemos -- plain [question]
                  dotnet run --project ConferenceDemos -- plan [--graph] [--rounds 1-5]
                  dotnet run --project ConferenceDemos -- decisions [openai-capture.json]
                  dotnet run --project ConferenceDemos -- decisions-record-openai <new-capture.json>
                  dotnet run --project ConferenceDemos -- speaker-page [--web] [--open] [--photo <file|https-url> | --no-photo]
                  dotnet run --project ConferenceDemos -- check-data
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
                Console.WriteLine("Step 2: Workflow basics (no model calls)\nPrepareAttendee -> CreateWelcome\n");
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
                Console.WriteLine("Step 1: Agent + tool\nWeather is fixed demo data, not a live forecast.\n");
                using var agentClient = FoundryChatClientFactory.CreateChatClient(FoundryChatClientFactory.LoadSettings());
                await AgentDemo.RunAsync(agentClient, Console.Out, cancellation.Token);
                return 0;
            }
            if (command == "check-data")
            {
                if (options.Length != 0)
                {
                    throw new ArgumentException("check-data takes no arguments.");
                }
                var schedule = new ScheduleCorpus();
                _ = DecisionDemo.ReadCapture(DecisionDemo.SamplePath, DecisionDemo.State());
                _ = SpeakerPageDemo.LoadProfile();
                _ = AgendaPlanningDemo.BuildWorkflow(null, schedule);
                Console.WriteLine($"{schedule.Sessions.Count} schedule sessions loaded from the embedded canonical fixture; " +
                    "decision JSON sample, agenda workflow graph, speaker profile, and page skill resolved.");
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
                Console.WriteLine("Shared session and attendee interest:\n" + state);
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

            if (command is not ("rag" or "plain"))
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
            switch (command)
            {
                case "rag":
                    Console.WriteLine("Step 3: Schedule RAG workflow\nPrepareQuestion -> AnswerFromSchedule\n");
                    Console.WriteLine(await ScheduleWorkflowDemo.RunAsync(
                        client, question, cancellation.Token, PrintWorkflowEvent));
                    break;
                case "plain":
                    var agent = new ChatClientAgent(client, name: "NoSchedule",
                        instructions: "You have no current Update Conference schedule. State this limitation explicitly; do not invent event sessions or times.");
                    Console.WriteLine((await agent.RunAsync(question,
                        cancellationToken: cancellation.Token)).Text);
                    break;
            }
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
        var graphOnly = false;
        for (var i = 0; i < options.Length; i++)
        {
            switch (options[i])
            {
                case "--graph":
                    graphOnly = true;
                    break;
                case "--rounds" when i + 1 < options.Length && int.TryParse(options[i + 1], out var rounds) && rounds is >= 1 and <= 5:
                    request = request with { MaxIterations = rounds };
                    i++;
                    break;
                default:
                    throw new ArgumentException("plan accepts --graph and --rounds 1-5.");
            }
        }

        var schedule = new ScheduleCorpus();
        Console.WriteLine("Planning + goal monitoring workflow (Mermaid, generated by WorkflowVisualizer):\n");
        Console.WriteLine(WorkflowVisualizer.ToMermaidString(AgendaPlanningDemo.BuildWorkflow(null, schedule)));
        if (graphOnly)
        {
            return 0;
        }

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
        var web = false;
        var open = false;
        var noPhoto = false;
        string? photo = null;
        for (var i = 0; i < options.Length; i++)
        {
            switch (options[i])
            {
                case "--web": web = true; break;
                case "--open": open = true; break;
                case "--no-photo": noPhoto = true; break;
                case "--photo" when i + 1 < options.Length && !string.IsNullOrWhiteSpace(options[i + 1]):
                    photo = options[++i];
                    break;
                default:
                    throw new ArgumentException("speaker-page accepts --web, --open, and --photo <file|https-url> or --no-photo.");
            }
        }
        if (noPhoto && photo is not null)
        {
            throw new ArgumentException("Use either --photo or --no-photo.");
        }

        var profile = SpeakerPageDemo.LoadProfile();
        var request = AgendaPlanningDemo.DefaultRequest;
        var output = SpeakerPageDemo.DefaultOutputDirectory;
        Console.WriteLine($"""
            Harness agent: build {profile.Name}'s speaker agenda page.
            Harness: todos + loop evaluator, skill 'speaker-agenda-page', sandboxed file tools ({output}),
                     schedule tools, CheckAgenda goals, web search {(web ? "ON (Responses API)" : "off")},
                     budget {SpeakerPageDemo.MaximumModelCalls} model calls / {SpeakerPageDemo.Deadline.TotalMinutes:0} min.

            """);
        var photoFile = await SpeakerPageDemo.PreparePhotoAsync(noPhoto ? null : photo ?? profile.Photo, output, Console.WriteLine, cancellationToken);
        var settings = FoundryChatClientFactory.LoadSettings();
        using var client = web ? SpeakerPageDemo.CreateWebSearchClient(settings) : FoundryChatClientFactory.CreateChatClient(settings);
        Console.WriteLine("\nTool calls:");
        var result = await SpeakerPageDemo.RunAsync(client, profile, request, output, photoFile, web, Console.WriteLine, cancellationToken);

        Console.WriteLine("\nTodo plan (recorded by the agent):");
        foreach (var todo in result.Todos)
        {
            Console.WriteLine($"  [{(todo.Done ? "x" : " ")}] {todo.Title}");
        }
        Console.WriteLine($"\nAgent summary:\n{result.Answer.Trim()}\n\nCode-owned acceptance checks:");
        foreach (var check in result.Checks)
        {
            Console.WriteLine($"  {(check.Passed ? "PASS" : "FAIL")}  {check.Check,-15} {check.Evidence}");
        }
        Console.WriteLine($"\nModel calls: {result.ModelCalls}/{SpeakerPageDemo.MaximumModelCalls}; tool calls: {result.ToolCalls.Count}\nPage: {result.HtmlPath}");
        if (open && File.Exists(result.HtmlPath))
        {
            SpeakerPageDemo.Open(result.HtmlPath);
        }
        Pause();
        return result.Passed ? 0 : 1;
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
