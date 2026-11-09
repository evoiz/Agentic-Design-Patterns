using AgenticDesignPatterns.Shared;
using ResourceOptimization;
using ResourceOptimization.Demos;
using ResourceOptimization.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

string? demoSelection = null;
string? deploymentSelection = null;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (deploymentSelection is not null ||
            index + 1 >= args.Length ||
            string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        deploymentSelection = args[++index];
    }
    else if (demoSelection is null)
    {
        demoSelection = args[index];
    }
    else
    {
        invalidArguments = true;
        break;
    }
}

var runAllDemos = string.Equals(demoSelection, "all", StringComparison.OrdinalIgnoreCase);
var runComplexity = runAllDemos ||
    string.Equals(demoSelection, "complexity", StringComparison.OrdinalIgnoreCase);
var runIntent = runAllDemos ||
    string.Equals(demoSelection, "research", StringComparison.OrdinalIgnoreCase);

if (invalidArguments || (!runComplexity && !runIntent))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/ResourceOptimization " +
        "[all|complexity|research] [--deployment <name>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    var fallbackDeployment = settings.ResolveModelDeployment(deploymentSelection);
    var routeDeployments = Enum.GetValues<OptimizationCategory>()
        .ToDictionary(category => category, category => ResolveRouteDeployment(settings, category, fallbackDeployment));
    var clientsByDeployment = routeDeployments.Values
        .Append(fallbackDeployment)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            deployment => deployment,
            deployment => FoundryChatClientFactory.CreateChatClient(
                settings with { ModelDeployment = deployment, ModelDeployments = [] }),
            StringComparer.OrdinalIgnoreCase);
    var routeClients = routeDeployments.ToDictionary(
        entry => entry.Key,
        entry => new OptimizationModelClient(entry.Value, clientsByDeployment[entry.Value]));
    var configuration = new ConfigurationBuilder()
        .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
        .AddEnvironmentVariables()
        .Build();
    using var searchClient = GoogleCustomSearchClient.FromConfiguration(configuration);
    var classifierClient = clientsByDeployment[fallbackDeployment];
    Console.WriteLine($"Classifier/default deployment: {fallbackDeployment}");
    foreach (var (category, modelClient) in routeClients)
    {
        Console.WriteLine($"{category} route deployment: {modelClient.Deployment}");
    }

    if (runComplexity)
    {
        Console.WriteLine("\n=== Complexity-based model routing ===");
        await ResourceOptimizationDemos.RunComplexityAsync(classifierClient, routeClients);
    }

    if (runIntent)
    {
        Console.WriteLine("\n=== Intent classification and search routing ===");
        await ResourceOptimizationDemos.RunIntentClassificationAsync(
            classifierClient,
            routeClients,
            searchClient);
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or HttpRequestException)
{
    Console.Error.WriteLine(DescribeException(exception));
    return 1;
}

return 0;

static string ResolveRouteDeployment(
    FoundrySettings settings,
    OptimizationCategory category,
    string fallbackDeployment)
{
    var alias = category switch
    {
        OptimizationCategory.Simple => "fast",
        OptimizationCategory.Reasoning => "reasoning",
        OptimizationCategory.InternetSearch => "quality",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
    return ResourceOptimizationWorkflow.ResolveDeploymentAlias(
        settings.ModelDeployments,
        alias,
        fallbackDeployment);
}

static string DescribeException(Exception exception)
{
    var messages = new List<string>();
    for (var current = exception; current is not null; current = current.InnerException)
    {
        messages.Add($"{current.GetType().Name}: {current.Message}");
    }

    return string.Join(" --> ", messages);
}
