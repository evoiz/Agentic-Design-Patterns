using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter12.ExceptionHandling;

public enum RecoveryRoute
{
    RetryPrimary,
    UseFallback,
    Respond
}

public sealed record ExceptionHandlingState(
    string OriginalRequest,
    string Address,
    int MaxPrimaryAttempts,
    int PrimaryAttempts = 0,
    string? Failure = null,
    string? PreciseResult = null,
    string? FallbackResult = null,
    RecoveryRoute? Route = null);

public static class ExceptionHandlingWorkflow
{
    public const string TelemetrySourceName = "Chapter12.ExceptionHandling";
    public const int MaxPrimaryAttempts = 2;

    public static AIAgent[] CreateAgents(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return
        [
            CreateAgent(
                chatClient,
                "PrimaryLocationHandler",
                """
                Explain the supplied precise-location record clearly. Use only the address, coordinates, and source supplied
                in the input. Do not infer or invent additional location details.
                """),
            CreateAgent(
                chatClient,
                "FallbackAreaHandler",
                """
                Explain the supplied general-area record and state that precise lookup was unavailable. Use only the supplied
                city, summary, and landmark; do not invent location facts.
                """),
            CreateAgent(
                chatClient,
                "LocationResponseHandler",
                """
                Return a concise answer from the supplied handler result. Preserve whether precise lookup succeeded or the
                area fallback was used. Do not add facts that are not present in the supplied result.
                """)
        ];
    }

    public static Workflow Create(
        AIAgent primaryHandler,
        AIAgent fallbackHandler,
        AIAgent responseHandler,
        Action<string>? writeOutput = null)
    {
        ArgumentNullException.ThrowIfNull(primaryHandler);
        ArgumentNullException.ThrowIfNull(fallbackHandler);
        ArgumentNullException.ThrowIfNull(responseHandler);

        var primary = new PrimaryLookupExecutor(primaryHandler, writeOutput);
        var decide = new RecoveryDecisionExecutor(writeOutput);
        var fallback = new FallbackLookupExecutor(fallbackHandler, writeOutput);
        var response = new ResponseExecutor(responseHandler, writeOutput);

        return new WorkflowBuilder(primary)
            .AddEdge(primary, decide)
            .AddEdge<ExceptionHandlingState>(decide, primary, state => state is { Route: RecoveryRoute.RetryPrimary })
            .AddEdge<ExceptionHandlingState>(decide, fallback, state => state is { Route: RecoveryRoute.UseFallback })
            .AddEdge<ExceptionHandlingState>(decide, response, state => state is { Route: RecoveryRoute.Respond })
            .AddEdge(fallback, response)
            .WithOutputFrom(response)
            .Build();
    }

    public static async Task<string> RunAsync(
        IChatClient chatClient,
        string request,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        cancellationToken.ThrowIfCancellationRequested();

        var address = LocationLookupService.ParseAddress(request);
        var agents = CreateAgents(chatClient);
        var workflow = Create(agents[0], agents[1], agents[2], writeOutput);
        var initialState = new ExceptionHandlingState(request.Trim(), address, MaxPrimaryAttempts);

        await using var run = await InProcessExecution.RunAsync(
            workflow,
            initialState,
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        string? finalOutput = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case ExecutorFailedEvent failedEvent:
                    var details = failedEvent.Data is Exception exception
                        ? exception.ToString()
                        : failedEvent.Data?.ToString() ?? "No exception details were provided.";
                    throw new InvalidOperationException(
                        $"Exception-handling executor '{failedEvent.ExecutorId}' failed: {details}",
                        failedEvent.Data as Exception);
                case WorkflowErrorEvent errorEvent:
                    throw new InvalidOperationException(
                        "The exception-handling workflow failed.",
                        errorEvent.Exception);
                case WorkflowOutputEvent outputEvent:
                    if (finalOutput is not null)
                    {
                        throw new InvalidOperationException(
                            "The exception-handling workflow produced multiple terminal results.");
                    }

                    finalOutput = outputEvent.As<string>();
                    break;
            }
        }

        return !string.IsNullOrWhiteSpace(finalOutput)
            ? finalOutput
            : throw new InvalidOperationException(
                "The exception-handling workflow completed without a final response.");
    }

    private static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions) =>
        new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();

    private sealed class PrimaryLookupExecutor(AIAgent primaryHandler, Action<string>? writeOutput)
        : Executor<ExceptionHandlingState, ExceptionHandlingState>("PrimaryLookup")
    {
        public override async ValueTask<ExceptionHandlingState> HandleAsync(
            ExceptionHandlingState state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.PrimaryAttempts >= state.MaxPrimaryAttempts || state.PreciseResult is not null)
            {
                throw new InvalidOperationException("The primary lookup cannot run outside its retry budget.");
            }

            var attempt = state.PrimaryAttempts + 1;
            writeOutput?.Invoke($"\n--- Precise location lookup (attempt {attempt} of {state.MaxPrimaryAttempts}) ---");
            try
            {
                var location = LocationLookupService.GetPreciseLocation(state.OriginalRequest, attempt);
                var handlerResponse = await primaryHandler.RunAsync(
                    $"""
                    Address: {location.Address}
                    Coordinates: {location.Coordinates}
                    Source: {location.Source}
                    """,
                    cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(handlerResponse.Text))
                {
                    throw new InvalidOperationException("The primary location handler returned an empty response.");
                }

                writeOutput?.Invoke($"Lookup result: {location.Address} ({location.Coordinates})");
                writeOutput?.Invoke($"\n--- Checkpoint: primary lookup succeeded on attempt {attempt} ---");
                return state with
                {
                    PrimaryAttempts = attempt,
                    PreciseResult = handlerResponse.Text,
                    Failure = null,
                    Route = RecoveryRoute.Respond
                };
            }
            catch (LocationLookupUnavailableException exception)
            {
                writeOutput?.Invoke($"Lookup error: {exception.Message}");
                writeOutput?.Invoke($"\n--- Checkpoint: primary attempt {attempt} failed ---");
                return state with
                {
                    PrimaryAttempts = attempt,
                    Failure = exception.Message,
                    Route = attempt < state.MaxPrimaryAttempts
                        ? RecoveryRoute.RetryPrimary
                        : RecoveryRoute.UseFallback
                };
            }
        }
    }

    private sealed class RecoveryDecisionExecutor(Action<string>? writeOutput)
        : Executor<ExceptionHandlingState, ExceptionHandlingState>("RecoveryDecision")
    {
        public override ValueTask<ExceptionHandlingState> HandleAsync(
            ExceptionHandlingState state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.Route is not RecoveryRoute route)
            {
                throw new InvalidOperationException("The recovery decision has no validated route.");
            }

            writeOutput?.Invoke($"\n--- Recovery decision: {route} ---");
            return ValueTask.FromResult(state);
        }
    }

    private sealed class FallbackLookupExecutor(AIAgent fallbackHandler, Action<string>? writeOutput)
        : Executor<ExceptionHandlingState, ExceptionHandlingState>("FallbackLookup")
    {
        public override async ValueTask<ExceptionHandlingState> HandleAsync(
            ExceptionHandlingState state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.Route != RecoveryRoute.UseFallback || string.IsNullOrWhiteSpace(state.Failure))
            {
                throw new InvalidOperationException("The fallback handler requires an exhausted primary lookup.");
            }

            var city = LocationLookupService.ExtractCity(state.Address);
            var area = LocationLookupService.GetGeneralArea(city);
            var sourceData = area is null
                ? $"City: {city}\nGeneral area result: No sample area record is available."
                : $"City: {area.City}\nSummary: {area.Summary}\nLandmark: {area.Landmark}";
            var handlerResponse = await fallbackHandler.RunAsync(
                $"""
                Primary lookup failure: {state.Failure}
                {sourceData}
                """,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(handlerResponse.Text))
            {
                throw new InvalidOperationException("The fallback location handler returned an empty response.");
            }

            writeOutput?.Invoke("\n--- General-area fallback ---");
            writeOutput?.Invoke(handlerResponse.Text);
            writeOutput?.Invoke("\n--- Checkpoint: fallback result recorded ---");
            return state with { FallbackResult = handlerResponse.Text };
        }
    }

    [YieldsOutput(typeof(string))]
    private sealed class ResponseExecutor(AIAgent responseHandler, Action<string>? writeOutput)
        : Executor<ExceptionHandlingState>("Response")
    {
        public override async ValueTask HandleAsync(
            ExceptionHandlingState state,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = state.PreciseResult is not null
                ? $"Precise lookup succeeded after {state.PrimaryAttempts} attempt(s).\n{state.PreciseResult}"
                : state.FallbackResult
                    ?? throw new InvalidOperationException("Recovery produced no result to present.");
            var response = await responseHandler.RunAsync(
                $"Original request: {state.Address}\nRecovered result:\n{source}",
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException("The final response handler returned an empty response.");
            }

            writeOutput?.Invoke("\n--- Final recovered response ---");
            writeOutput?.Invoke(response.Text);
            await context.YieldOutputAsync(response.Text, cancellationToken);
        }
    }
}
