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
                  dotnet run --project ConferenceDemos -- shopping [preference] [--incomplete]
                  dotnet run --project ConferenceDemos -- decisions [openai-capture.json]
                  dotnet run --project ConferenceDemos -- decisions-record-openai <new-capture.json>
                  dotnet run --project ConferenceDemos -- research [request]
                  dotnet run --project ConferenceDemos -- check-data
                See README.md for credentials, recording, and project-local hosted deployment.
                """);
            return 0;
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
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
                var sessions = new ScheduleCorpus().Sessions;
                _ = DecisionDemo.ReadCapture(DecisionDemo.SamplePath, DecisionDemo.State());
                _ = SpeakerDemo.Sources();
                Console.WriteLine($"{sessions.Count} schedule sessions loaded from the embedded canonical fixture; decision JSON sample and speaker sources resolved.");
                return 0;
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

            if (command is not ("rag" or "plain" or "shopping" or "research"))
            {
                throw new ArgumentException($"Unknown command '{command}'. Use --help.");
            }
            if (options.Any(o => o.StartsWith("--", StringComparison.Ordinal) && !(command == "shopping" && o == "--incomplete")))
            {
                throw new ArgumentException("Unsupported option. Use --help for the supported command shape.");
            }
            var question = options.Length == 0
                ? command == "research" ? SpeakerDemo.DefaultRequest : ScheduleDemo.DefaultQuestion
                : string.Join(" ", options);
            if (command != "shopping")
            {
                Console.WriteLine($"User question: {question}\n");
            }
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
                case "shopping":
                    var preferences = options.Where(o => o != "--incomplete").ToArray();
                    if (preferences.Length > 1)
                    {
                        throw new ArgumentException("Shopping accepts one preference and optional --incomplete.");
                    }
                    var request = ShoppingDemo.Sample(preferences.SingleOrDefault() ?? "vegetarian");
                    if (options.Contains("--incomplete"))
                    {
                        request = request with { Pantry = request.Pantry.ToDictionary(p => p.Key, p => p.Key == "tomato" ? null : p.Value) };
                    }
                    Console.WriteLine($"User request: Choose a {request.Preference} dinner and list only the missing ingredients.\n" +
                        $"Pantry (true = present, false = missing, null = unknown):\n{JsonSerializer.Serialize(request.Pantry, DecisionDemo.JsonOptions)}\n");
                    var shopping = await ShoppingDemo.RunAsync(client, request, cancellation.Token);
                    Console.WriteLine(shopping.Status);
                    if (!shopping.Passed)
                    {
                        return 1;
                    }
                    Console.WriteLine($"Meal: {shopping.Meal}\nShopping list (missing only):\n" +
                        (shopping.Missing.Count == 0 ? "Nothing to buy." : string.Join("\n", shopping.Missing)));
                    break;
                case "research":
                    Console.WriteLine(SpeakerDemo.Format(await SpeakerDemo.RunAsync(client, question, cancellation.Token)));
                    break;
            }
            Console.ReadKey();
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or
            JsonException or HttpRequestException or OperationCanceledException or ElBruno.AI.Decisions.DecisionException or System.ClientModel.ClientResultException)
        {
            Console.Error.WriteLine($"Demo failed: {ex.Message}");
            return 1;
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
