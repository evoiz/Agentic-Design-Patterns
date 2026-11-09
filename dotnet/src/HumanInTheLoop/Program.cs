using AgenticDesignPatterns.Shared;
using HumanInTheLoop;
using Microsoft.Extensions.AI;

const string request =
    "My new wireless headphones keep disconnecting after the basic troubleshooting steps. " +
    "This is a complex issue and I need more help.";
var customer = new SupportCustomer
{
    Name = "Sam",
    Tier = "premium",
    RecentPurchases = ["Wireless headphones"],
    SupportHistory = ["Previously contacted support about Bluetooth pairing."]
};
string? deploymentAlias = null;
var requestParts = new List<string>();
for (var index = 0; index < args.Length; index++)
{
    if (args[index].Equals("--deployment", StringComparison.OrdinalIgnoreCase) && deploymentAlias is null &&
        index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]))
    {
        deploymentAlias = args[++index];
    }
    else if (args[index].StartsWith("--", StringComparison.Ordinal))
    {
        Console.Error.WriteLine(
            "Usage: dotnet run --project dotnet/src/HumanInTheLoop [<support request>] [--deployment <name>]");
        return 1;
    }
    else
    {
        requestParts.Add(args[index]);
    }
}
var requestText = requestParts.Count == 0 ? request : string.Join(' ', requestParts);

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
    Console.WriteLine($"Using model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
    Console.WriteLine("--- Customer profile ---");
    Console.WriteLine($"{customer.Name} ({customer.Tier})");
    Console.WriteLine($"Recent purchases: {string.Join(", ", customer.RecentPurchases)}");
    Console.WriteLine($"Support history: {string.Join(", ", customer.SupportHistory)}");

    var agent = CustomerSupportAgent.Create(chatClient, customer, Console.WriteLine);
    var session = await agent.CreateSessionAsync();
    Console.WriteLine($"\nUser: {requestText}");
    var response = await agent.RunAsync(requestText, session);

    while (true)
    {
        var pendingApprovals = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>()
            .ToArray();
        if (pendingApprovals.Length == 0)
        {
            break;
        }

        var approvalResponses = new List<AIContent>(pendingApprovals.Length);
        foreach (var pendingApproval in pendingApprovals)
        {
            Console.WriteLine("\n--- Human approval required ---");
            Console.WriteLine($"Tool: {pendingApproval.ToolCall}");
            Console.Write("Approve this action? [y/N]: ");
            var approved = string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase);
            approvalResponses.Add(pendingApproval.CreateResponse(approved));
        }

        var approvalMessage = new ChatMessage(ChatRole.User, approvalResponses);
        response = await agent.RunAsync(approvalMessage, session);
    }

    if (string.IsNullOrWhiteSpace(response.Text))
    {
        throw new InvalidOperationException("The support agent returned an empty response.");
    }

    Console.WriteLine("\n--- Final support response ---");
    Console.WriteLine(response.Text);
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
