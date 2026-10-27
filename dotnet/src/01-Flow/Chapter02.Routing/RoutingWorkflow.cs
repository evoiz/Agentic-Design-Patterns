using Chapter02.Routing.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter02.Routing;

public static class RoutingWorkflow
{
    public const string TelemetrySourceName = "Chapter02.Routing";

    public static Workflow Create(IChatClient chatClient, Action<string>? writeOutput = null)
    {
        var routerAgent = new ChatClientAgent(
            chatClient,
            """
            You are the main coordinator. Your only task is to analyze incoming user requests and decide which specialist should handle them.
            - For any requests related to booking flights or hotels, respond with BOOKER.
            - For all other general information questions, respond with INFO.
            - If the request is unclear or doesn't fit either category, respond with UNCLEAR.
            After the route token, add a short rationale on the next line using the format REASON: <why>.
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
                    ? DescribeException(exception)
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
                        : $"The workflow failed: {DescribeException(errorEvent.Exception)}",
                    errorEvent.Exception);
            }
        }

        return !string.IsNullOrWhiteSpace(finalOutput)
            ? finalOutput
            : throw new InvalidOperationException("The routing workflow completed without a final output.");
    }

    private static string DescribeException(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join(" --> ", messages);
    }

    private sealed class RouterExecutor(AIAgent routerAgent, Action<string>? writeOutput)
        : Executor<ChatMessage, RoutedRequest>("RouterExecutor")
    {
        public override async ValueTask<RoutedRequest> HandleAsync(
            ChatMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var response = await routerAgent.RunAsync(message, cancellationToken: cancellationToken);
            var (decision, rationale) = ParseDecision(response.Text);

            writeOutput?.Invoke("\n--- Coordinator router decision ---");
            writeOutput?.Invoke($"Raw output: {response.Text}");
            writeOutput?.Invoke($"Route: {decision}");
            writeOutput?.Invoke($"Reason: {rationale}");

            return new RoutedRequest(message.Text ?? string.Empty, decision, response.Text, rationale);
        }

        private static (RoutingDecision Decision, string Rationale) ParseDecision(string rawOutput)
        {
            var lines = rawOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var routeToken = lines.FirstOrDefault() ?? string.Empty;
            var rationale = lines
                .Skip(1)
                .FirstOrDefault(line => line.StartsWith("REASON:", StringComparison.OrdinalIgnoreCase));

            var normalizedRoute = routeToken.Trim().TrimEnd('.', ':').ToUpperInvariant();
            var decision = normalizedRoute switch
            {
                "BOOKER" => RoutingDecision.Booker,
                "INFO" => RoutingDecision.Info,
                _ => RoutingDecision.Unclear
            };

            var parsedRationale = rationale is null
                ? "No rationale provided."
                : rationale["REASON:".Length..].Trim();

            return (decision, parsedRationale);
        }
    }

    private sealed class SpecialistExecutor : Executor<RoutedRequest, string>
    {
        private readonly RoutingDecision _expectedDecision;
        private readonly AIAgent _agent;
        private readonly string _header;
        private readonly string _executorId;
        private readonly Action<string>? _writeOutput;

        public SpecialistExecutor(
            string executorId,
            RoutingDecision expectedDecision,
            AIAgent agent,
            string header,
            Action<string>? writeOutput) : base(executorId)
        {
            _expectedDecision = expectedDecision;
            _agent = agent;
            _header = header;
            _executorId = executorId;
            _writeOutput = writeOutput;
        }

        public override async ValueTask<string> HandleAsync(
            RoutedRequest message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            if (message.Decision != _expectedDecision)
            {
                throw new InvalidOperationException(
                    $"{_executorId} cannot handle decision '{message.Decision}'.");
            }

            var response = await _agent.RunAsync(message.OriginalRequest, cancellationToken: cancellationToken);

            _writeOutput?.Invoke($"\n--- {_header} ---");
            _writeOutput?.Invoke(response.Text);

            return response.Text;
        }
    }

    [YieldsOutput(typeof(string))]
    private sealed class ClarificationExecutor(Action<string>? writeOutput) : Executor<RoutedRequest>("ClarificationExecutor")
    {
        public override async ValueTask HandleAsync(
            RoutedRequest message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            const string clarification = "Coordinator could not delegate the request confidently. Please clarify whether you need travel booking help or general information.";

            writeOutput?.Invoke("\n--- Clarification fallback ---");
            writeOutput?.Invoke(clarification);

            await context.YieldOutputAsync(clarification, cancellationToken);
        }
    }
}
