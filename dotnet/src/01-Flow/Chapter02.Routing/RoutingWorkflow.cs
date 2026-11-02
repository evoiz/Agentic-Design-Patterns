using System.Text.Json;
using System.Text.Json.Serialization;
using Chapter02.Routing.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter02.Routing;

public static class RoutingWorkflow
{
    public const string TelemetrySourceName = "Chapter02.Routing";
    private static readonly JsonSerializerOptions RoutingJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<RoutingDecision>(allowIntegerValues: false) }
    };

    public static Workflow Create(IChatClient chatClient, Action<string>? writeOutput = null)
    {
        var routerAgent = new ChatClientAgent(
            chatClient,
            """
            You are the main coordinator. Your only task is to analyze incoming user requests and decide which specialist should handle them.
            - For any requests related to booking flights or hotels, select Booker.
            - For all other general information questions, select Info.
            - If the request is unclear or doesn't fit either category, select Unclear.
            Return a structured decision and a short rationale.
            Do not answer the user's request directly.
            """,
            name: "CoordinatorRouter")
            .AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();

        var bookerAgent = new ChatClientAgent(
            chatClient,
            """
            You are the Booker specialist from a routing demo.
            The demo simulates bookings rather than completing real reservations.
            For each request, briefly confirm the travel booking task, mention the destination or itinerary you extracted, and state that the booking action was simulated.
            Keep the response to 2 sentences maximum.
            """,
            name: "Booker")
            .AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();

        var infoAgent = new ChatClientAgent(
            chatClient,
            """
            You are the Info specialist from a routing demo.
            Answer general information requests clearly and concisely in 1-2 sentences.
            If the user asks for factual information, provide the answer directly.
            """,
            name: "Info")
            .AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();

        var routerExecutor = new RouterExecutor(routerAgent, writeOutput);
        var bookingExecutor = new SpecialistExecutor(
            "BookerExecutor",
            RoutingDecision.Booker,
            bookerAgent,
            header: "Booker specialist output",
            writeOutput);
        var infoExecutor = new SpecialistExecutor(
            "InfoExecutor",
            RoutingDecision.Info,
            infoAgent,
            header: "Info specialist output",
            writeOutput);
        var clarificationExecutor = new ClarificationExecutor(writeOutput);

        return new WorkflowBuilder(routerExecutor)
            .AddEdge<RoutedRequest>(routerExecutor, bookingExecutor, route => route is { Decision: RoutingDecision.Booker })
            .AddEdge<RoutedRequest>(routerExecutor, infoExecutor, route => route is { Decision: RoutingDecision.Info })
            .AddEdge<RoutedRequest>(routerExecutor, clarificationExecutor, route => route is { Decision: RoutingDecision.Unclear })
            .WithOutputFrom(bookingExecutor, infoExecutor, clarificationExecutor)
            .Build();
    }

    public static async Task<string> RunAsync(
        IChatClient chatClient,
        string request,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var workflow = Create(chatClient, writeOutput);
        await using var run = await InProcessExecution.RunAsync(
            workflow, new ChatMessage(ChatRole.User, request), cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        string? finalOutput = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            if (workflowEvent is WorkflowOutputEvent outputEvent)
            {
                finalOutput = outputEvent.As<string>();
            }
            else if (workflowEvent is ExecutorFailedEvent failedEvent)
            {
                var details = failedEvent.Data is Exception exception
                    ? exception.Message
                    : "No exception details were provided.";
                throw new InvalidOperationException(
                    $"Workflow executor '{failedEvent.ExecutorId}' failed: {details}",
                    failedEvent.Data as Exception);
            }
            else if (workflowEvent is WorkflowErrorEvent errorEvent)
            {
                throw new InvalidOperationException(
                    errorEvent.Exception is null
                        ? "The workflow reported an error without exception details."
                        : $"The workflow failed: {errorEvent.Exception.Message}",
                    errorEvent.Exception);
            }
        }

        return !string.IsNullOrWhiteSpace(finalOutput)
            ? finalOutput
            : throw new InvalidOperationException("The routing workflow completed without a final output.");
    }

    private sealed class RouterExecutor(AIAgent routerAgent, Action<string>? writeOutput)
        : Executor<ChatMessage, RoutedRequest>("RouterExecutor")
    {
        public override async ValueTask<RoutedRequest> HandleAsync(
            ChatMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var response = await routerAgent.RunAsync<RoutingAssessment>(
                message, serializerOptions: RoutingJson, cancellationToken: cancellationToken);
            var assessment = response.Result;
            if (assessment?.Decision is not { } decision || !Enum.IsDefined(decision) ||
                string.IsNullOrWhiteSpace(assessment.Rationale))
            {
                throw new InvalidOperationException("The router returned an incomplete or unsupported structured decision.");
            }

            writeOutput?.Invoke("\n--- Coordinator router decision ---");
            writeOutput?.Invoke($"Route: {decision}");
            writeOutput?.Invoke($"Reason: {assessment.Rationale}");
            return new RoutedRequest(message.Text ?? string.Empty, decision);
        }
    }

    private sealed class SpecialistExecutor(
        string executorId,
        RoutingDecision expectedDecision,
        AIAgent agent,
        string header,
        Action<string>? writeOutput) : Executor<RoutedRequest, string>(executorId)
    {
        public override async ValueTask<string> HandleAsync(
            RoutedRequest message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            if (message.Decision != expectedDecision)
            {
                throw new InvalidOperationException(
                    $"{Id} cannot handle decision '{message.Decision}'.");
            }

            var response = await agent.RunAsync(message.OriginalRequest, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException($"{Id} returned an empty response.");
            }

            writeOutput?.Invoke($"\n--- {header} ---");
            writeOutput?.Invoke(response.Text);

            return response.Text;
        }
    }

    private sealed class ClarificationExecutor(Action<string>? writeOutput) : Executor<RoutedRequest, string>("ClarificationExecutor")
    {
        public override ValueTask<string> HandleAsync(
            RoutedRequest message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            const string clarification = "Coordinator could not delegate the request confidently. Please clarify whether you need travel booking help or general information.";

            writeOutput?.Invoke("\n--- Clarification fallback ---");
            writeOutput?.Invoke(clarification);

            return ValueTask.FromResult(clarification);
        }
    }
}
