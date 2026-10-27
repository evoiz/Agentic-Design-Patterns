using AgenticDesignPatterns.Shared;
using Azure.Identity;
using Chapter19.Evaluation.Demos;

var selection = "all";
string? deploymentAlias = null;
var selectionSeen = false;
for (var index = 0; index < args.Length; index++)
{
    if (args[index] == "--deployment" && deploymentAlias is null &&
        index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]) &&
        !args[index + 1].StartsWith("--", StringComparison.Ordinal))
    {
        deploymentAlias = args[++index];
    }
    else if (!selectionSeen && args[index] is "basic" or "judge" or "meai" or "foundry" or "all")
    {
        selection = args[index];
        selectionSeen = true;
    }
    else
    {
        Console.Error.WriteLine("Usage: Chapter19.Evaluation [basic|judge|meai|foundry|all] [--deployment <alias>]");
        return 1;
    }
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    var deployment = settings.ResolveModelDeployment(deploymentAlias);
    Console.WriteLine($"Using model deployment: {deployment}");
    if (selection == "foundry")
    {
        await FoundryEvaluationDemo.RunAsync(settings, deployment, cancellation.Token);
    }
    else
    {
        using var chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        if (selection is "basic" or "all")
            await BasicMetricsDemo.RunAsync(chatClient, cancellation.Token);
        if (selection is "judge" or "all")
            await LegalSurveyDemo.RunAsync(chatClient, cancellation.Token);
        if (selection is "meai" or "all")
            await MeaiEvaluationDemo.RunAsync(chatClient, cancellation.Token);
    }
}
catch (AuthenticationFailedException exception)
{
    Console.Error.WriteLine($"Foundry cloud evaluation requires Entra ID: sign in with az login. {exception.Message}");
    return 1;
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
    HttpRequestException or System.ClientModel.ClientResultException or System.Text.Json.JsonException or TimeoutException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Evaluation canceled.");
    return 1;
}
return 0;
