using AgenticDesignPatterns.Shared;
using GoalSettingMonitoring;
using GoalSettingMonitoring.Models;
using Microsoft.Extensions.AI;

const string Usage =
    """
    Usage: dotnet run --project dotnet/src/GoalSettingMonitoring [options]
      --scenario <dotnet|python>  Deliverable scenario (default: dotnet)
      --use-case <text>           Request description
      --goals <g1;g2;...>         Goals separated by semicolons
      --iterations <1-5>          Maximum generation/review rounds (default: 5)
      --output-dir <path>         Parent directory for generated artifact folder
      --deployment <alias>        Optional configured Foundry deployment alias
      --include-sensitive-telemetry  Opt in to tracing prompts and responses
    """;

var request = GoalSettingRequest.DotNetDeliverableSample;
string? outputDirectory = null;
string? deploymentAlias = null;
var enableSensitiveTelemetry = false;
var scenario = GoalSettingScenario.DotNetDeliverable;
var useCaseSupplied = false;
var goalsSupplied = false;
for (var index = 0; index < args.Length; index++)
{
    if (args[index] is "--help" or "-h")
    {
        Console.WriteLine(Usage);
        return 0;
    }

    var option = args[index];
    if (option == "--include-sensitive-telemetry")
    {
        enableSensitiveTelemetry = true;
        continue;
    }

    var value = index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]) ? args[++index] : null;
    var valid = value is not null;
    switch (option)
    {
        case "--scenario" when string.Equals(value, "dotnet", StringComparison.OrdinalIgnoreCase):
            scenario = GoalSettingScenario.DotNetDeliverable;
            break;
        case "--scenario" when string.Equals(value, "python", StringComparison.OrdinalIgnoreCase):
            scenario = GoalSettingScenario.PythonCode;
            break;
        case "--use-case" when valid:
            request = request with { UseCase = value! };
            useCaseSupplied = true;
            break;
        case "--goals" when valid:
            request = request with
            {
                Goals = value!.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            };
            goalsSupplied = true;
            break;
        case "--iterations" when int.TryParse(value, out var iterations):
            request = request with { MaxIterations = iterations };
            break;
        case "--output-dir" when valid:
            outputDirectory = value;
            break;
        case "--deployment" when valid && deploymentAlias is null:
            deploymentAlias = value;
            break;
        default:
            Console.Error.WriteLine(Usage);
            return 2;
    }
}

var scenarioDefaults = scenario == GoalSettingScenario.PythonCode
    ? GoalSettingRequest.BinaryGapSample
    : GoalSettingRequest.DotNetDeliverableSample;
request = scenarioDefaults with
{
    UseCase = useCaseSupplied ? request.UseCase : scenarioDefaults.UseCase,
    Goals = goalsSupplied ? request.Goals : scenarioDefaults.Goals,
    MaxIterations = request.MaxIterations
};

try
{
    request.Validate();
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
    await GoalSettingDemo.RunAsync(
        chatClient,
        request,
        outputDirectory,
        Console.WriteLine,
        enableSensitiveTelemetry: enableSensitiveTelemetry);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
