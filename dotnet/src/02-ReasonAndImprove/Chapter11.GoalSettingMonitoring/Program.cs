using AgenticDesignPatterns.Shared;
using Chapter11.GoalSettingMonitoring;
using Chapter11.GoalSettingMonitoring.Models;
using Microsoft.Extensions.AI;

const string Usage =
    """
    Usage: dotnet run --project dotnet/src/02-ReasonAndImprove/Chapter11.GoalSettingMonitoring [options]
      --use-case <text>        Code-generation request (default: binary-gap example)
      --goals <g1;g2;...>      Goals separated by semicolons
      --iterations <1-5>       Maximum generation/review rounds (default: 5)
      --output-dir <path>      Parent directory for generated-code/
      --deployment <alias>     Optional configured Foundry deployment alias
    """;

var request = GoalSettingRequest.BinaryGapSample;
string? outputDirectory = null;
string? deploymentAlias = null;
for (var index = 0; index < args.Length; index++)
{
    if (args[index] is "--help" or "-h")
    {
        Console.WriteLine(Usage);
        return 0;
    }

    var option = args[index];
    var value = index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]) ? args[++index] : null;
    var valid = value is not null;
    switch (option)
    {
        case "--use-case" when valid:
            request = request with { UseCase = value! };
            break;
        case "--goals" when valid:
            request = request with
            {
                Goals = value!.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            };
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

try
{
    request.Validate();
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
    await GoalSettingDemo.RunAsync(chatClient, request, outputDirectory, Console.WriteLine);
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
