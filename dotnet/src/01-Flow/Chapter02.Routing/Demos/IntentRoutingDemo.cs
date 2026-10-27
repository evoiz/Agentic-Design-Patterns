using Microsoft.Extensions.AI;

namespace Chapter02.Routing.Demos;

internal static class IntentRoutingDemo
{
    private static readonly (string Label, string Request)[] SampleRequests =
    [
        ("Booking", "Book me a hotel in Paris."),
        ("Information", "What is the highest mountain in the world?"),
        ("Unclear", "Can you help me with this?"),
        ("Booking", "Find flights to Tokyo next month.")
    ];

    public static async Task RunAsync(IChatClient chatClient)
    {
        foreach (var (label, request) in SampleRequests)
        {
            Console.WriteLine($"\n>>> Sample route: {label}");
            Console.WriteLine($">>> User request: {request}");
            var response = await RoutingWorkflow.RunAsync(chatClient, request, Console.WriteLine);
            Console.WriteLine("\n--- Final routed response ---");
            Console.WriteLine(response);
        }
    }
}
