using System.Text.Json;
using AgenticDesignPatterns.Shared;
using Extension02.DecisionModels;
using Microsoft.Extensions.Configuration;

if (args.Contains("--help"))
{
    Console.WriteLine("Extension02.DecisionModels [all|microsoft|openai|maf] [--provider microsoft|openai] [--input \"support request\"]");
    return;
}

var (variant, provider, input) = DemoArguments.Parse(args);
var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables().Build();
var settings = DecisionConfiguration.LoadSettings(configuration);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

string[] samples = input is null
    ? ["I was charged twice. Please return the extra payment.",
       "Checkout is down for all EU customers. Every API call returns 500.",
       "I cannot sign in after resetting my password."]
    : [input];
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

foreach (var selected in variant == "all" ? new[] { "microsoft", "openai" } :
         variant == "maf" ? Array.Empty<string>() : [variant])
{
    using var decisions = DecisionConfiguration.CreateClient(settings, selected);
    foreach (var sample in samples)
    {
        Console.WriteLine($"\n=== {selected}: {sample} ===");
        var report = await DecisionWorkflow.EvaluateAsync(decisions, sample, Console.WriteLine, cancellation.Token);
        Console.WriteLine("\n--- Deterministic disposition and priority ---");
        Console.WriteLine($"{report.Disposition}; {report.Priority}");
    }
}
if (variant is "all" or "maf")
{
    if (string.IsNullOrWhiteSpace(settings.ModelDeployment))
    {
        throw new InvalidOperationException("Foundry:ModelDeployment must select a generative chat model for MAF.");
    }
    using var decisions = DecisionConfiguration.CreateClient(settings, provider);
    using var chat = FoundryChatClientFactory.CreateChatClient(settings);
    foreach (var sample in samples)
    {
        Console.WriteLine($"\n=== MAF + {provider}: {sample} ===");
        var result = await DecisionAgentWorkflow.RunAsync(
            decisions, chat, sample, Console.WriteLine, cancellation.Token);
        Console.WriteLine("\n--- Final workflow result ---");
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
    }
}
