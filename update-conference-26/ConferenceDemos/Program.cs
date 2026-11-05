using System.Text.Json;
using AgenticDesignPatterns.Shared;
using Extension02.DecisionModels;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ConferenceDemos;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args is ["--help"])
        {
            Console.WriteLine("""
                Conference demos (run from update-conference-26):
                  dotnet run --project ConferenceDemos -- rag [--day 1|2] [question]
                  dotnet run --project ConferenceDemos -- plain [question]
                  dotnet run --project ConferenceDemos -- shopping [preference] [--incomplete]
                  dotnet run --project ConferenceDemos -- decisions <verified-openai-capture.json>
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
            if (command == "check-data")
            {
                if (options.Length != 0)
                {
                    throw new ArgumentException("check-data takes no arguments.");
                }
                var sessions = new ScheduleCorpus().Sessions;
                _ = DecisionDemo.Context();
                _ = SpeakerDemo.Sources();
                Console.WriteLine($"{sessions.Count} schedule sessions loaded from the embedded canonical fixture; decision and speaker sources resolved.");
                return 0;
            }
            if (command is "decisions" or "decisions-record-openai")
            {
                if (options.Length != 1 || string.IsNullOrWhiteSpace(options[0]))
                {
                    throw new ArgumentException("Supply exactly one capture file path.");
                }
                var context = DecisionDemo.Context();
                var capture = command == "decisions" ? DecisionDemo.ReadCapture(options[0], context) : null;
                var settings = FoundryChatClientFactory.LoadSettings();
                using var decisions = DecisionConfiguration.CreateClient(settings, command == "decisions" ? "microsoft" : "openai");
                var result = await DecisionDemo.EvaluateAsync(decisions, context, cancellation.Token);
                if (command == "decisions-record-openai")
                {
                    var recorded = DecisionDemo.Capture(result, context, settings.ResolveModelDeployment("openaiDecision"));
                    DecisionDemo.ValidateCapture(recorded, context);
                    var path = Path.GetFullPath(options[0]);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                    await JsonSerializer.SerializeAsync(file, recorded, DecisionDemo.JsonOptions, cancellation.Token);
                    Console.WriteLine("Recorded a live Azure OpenAI Decisions result for all three tasks. Review it before rehearsal.");
                }
                else
                {
                    Console.WriteLine("Context and tasks:\n" + JsonSerializer.Serialize(context, DecisionDemo.JsonOptions));
                    Console.WriteLine("\nMicrosoft-Decision-1 - LIVE (Choice / Noul / Score):\n" +
                        JsonSerializer.Serialize(result, DecisionDemo.JsonOptions));
                    Console.WriteLine($"\nAzure OpenAI Decisions - CAPTURED ({capture!.CapturedAt:O}, model {capture.ModelDeployment}):\n" +
                        JsonSerializer.Serialize(capture.Results, DecisionDemo.JsonOptions));
                    Console.WriteLine("\nAPI/output illustration only. Scores are not calibrated certainty or an accuracy benchmark.");
                }
                return 0;
            }

            if (command is not ("rag" or "plain" or "shopping" or "research"))
            {
                throw new ArgumentException($"Unknown command '{command}'. Use --help.");
            }
            var day = 1;
            if (command == "rag" && options.FirstOrDefault() == "--day")
            {
                if (options.Length < 2 || !int.TryParse(options[1], out day) || day is not (1 or 2))
                {
                    throw new ArgumentException("--day requires 1 or 2.");
                }
                options = options[2..];
            }
            if (options.Any(o => o.StartsWith("--", StringComparison.Ordinal) && !(command == "shopping" && o == "--incomplete")))
            {
                throw new ArgumentException("Unsupported option. Use --help for the supported command shape.");
            }
            using IChatClient client = FoundryChatClientFactory.CreateChatClient(FoundryChatClientFactory.LoadSettings());
            switch (command)
            {
                case "rag":
                    Console.WriteLine(await ScheduleDemo.RunAsync(client,
                        options.Length == 0 ? ScheduleDemo.DefaultQuestion : string.Join(" ", options), day, cancellation.Token));
                    break;
                case "plain":
                    var agent = new ChatClientAgent(client, name: "NoSchedule",
                        instructions: "You have no current Update Conference schedule. State this limitation explicitly; do not invent event sessions or times.");
                    Console.WriteLine((await agent.RunAsync(
                        options.Length == 0 ? ScheduleDemo.DefaultQuestion : string.Join(" ", options),
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
                    Console.WriteLine(SpeakerDemo.Format(await SpeakerDemo.RunAsync(client,
                        options.Length == 0 ? SpeakerDemo.DefaultRequest : string.Join(" ", options), cancellation.Token)));
                    break;
            }
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or
            JsonException or HttpRequestException or OperationCanceledException or ElBruno.AI.Decisions.DecisionException or System.ClientModel.ClientResultException)
        {
            Console.Error.WriteLine($"Demo failed: {ex.Message}");
            return 1;
        }
    }
}
