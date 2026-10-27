using AgenticDesignPatterns.Shared;
using Chapter11.GoalSettingMonitoring;
using Chapter11.GoalSettingMonitoring.Models;
using Microsoft.Extensions.AI;

var request = GoalSettingRequest.BinaryGapSample;
string? outputDirectory = null;
string? deploymentAlias = null;
var showHelp = false;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    var option = args[index];
    if (option is "--help" or "-h")
    {
        showHelp = true;
        break;
    }

    if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
    {
        invalidArguments = true;
        break;
    }

    var value = args[++index];
    switch (option)
    {
        case "--use-case":
            request = request with { UseCase = value };
            break;
        case "--goals":
            request = request with
            {
                Goals = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            };
            break;
        case "--iterations":
            if (!int.TryParse(value, out var iterations))
            {
                invalidArguments = true;
            }
            else
            {
                request = request with { MaxIterations = iterations };
            }

            break;
        case "--output-dir":
            outputDirectory = value;
            break;
        case "--deployment":
            if (deploymentAlias is not null)
            {
                invalidArguments = true;
            }
            else
            {
                deploymentAlias = value;
            }

            break;
        default:
            invalidArguments = true;
            break;
    }

    if (invalidArguments)
    {
        break;
    }
}

if (showHelp)
{
    Console.WriteLine(
        """
        Usage: dotnet run --project dotnet/src/02-ReasonAndImprove/Chapter11.GoalSettingMonitoring [options]
          --use-case <text>        Code-generation request (default: binary-gap example)
          --goals <g1;g2;...>      Goals separated by semicolons
          --iterations <1-5>       Maximum generation/review rounds (default: 5)
          --output-dir <path>      Parent directory for generated-code/
          --deployment <alias>     Optional configured Foundry deployment alias
        """);
    return 0;
}

if (invalidArguments)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/02-ReasonAndImprove/Chapter11.GoalSettingMonitoring " +
        "[--use-case <text>] [--goals <g1;g2;...>] [--iterations 1-5] " +
        "[--output-dir <path>] [--deployment <alias>]");
    return 2;
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
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"Foundry request failed: {exception.Message}");
    return 1;
}

return 0;
