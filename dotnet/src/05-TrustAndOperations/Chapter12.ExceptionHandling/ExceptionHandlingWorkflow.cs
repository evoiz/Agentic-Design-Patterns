using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Chapter12.ExceptionHandling;

public sealed record ExceptionHandlingState(
    string OriginalRequest,
    string Address,
    int MaxPrimaryAttempts,
    int PrimaryAttempts = 0,
    string? Failure = null,
    string? PreciseResult = null,
    string? FallbackResult = null)
{
    public bool ShouldRetry => PreciseResult is null && Failure is not null && PrimaryAttempts < MaxPrimaryAttempts;
    public bool ShouldFallback => PreciseResult is null && Failure is not null && PrimaryAttempts >= MaxPrimaryAttempts;
}

public static class ExceptionHandlingWorkflow
{
    public const string TelemetrySourceName = "Chapter12.ExceptionHandling";
    public const int MaxPrimaryAttempts = 2;

    // primary -(transient failure)-> primary; -(budget exhausted)-> fallback -> respond; -(success)-> respond.
    public static async Task<string> RunAsync(
        IChatClient chatClient,
        string request,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        cancellationToken.ThrowIfCancellationRequested();

        var primary = new PrimaryLookupExecutor(CreateAgent(chatClient, "PrimaryLocationHandler", """
            Explain the supplied precise-location record clearly. Use only the address, coordinates, and source supplied
            in the input. Do not infer or invent additional location details.
            """), writeOutput);
        var fallback = new FallbackLookupExecutor(CreateAgent(chatClient, "FallbackAreaHandler", """
            Explain the supplied general-area record and state that precise lookup was unavailable. Use only the supplied
            city, summary, and landmark; do not invent location facts.
            """), writeOutput);
        var respond = new ResponseExecutor(writeOutput);
        var workflow = new WorkflowBuilder(primary)
            .AddEdge<ExceptionHandlingState>(primary, primary, state => state is { ShouldRetry: true })
            .AddEdge<ExceptionHandlingState>(primary, fallback, state => state is { ShouldFallback: true })
            .AddEdge<ExceptionHandlingState>(primary, respond, state => state is { PreciseResult: not null })
            .AddEdge(fallback, respond)
            .WithOutputFrom(respond)
            .Build();

        var initialState = new ExceptionHandlingState(
            request.Trim(), LocationLookupService.ParseAddress(request), MaxPrimaryAttempts);
        await using var run = await InProcessExecution.RunAsync(workflow, initialState, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        string? finalOutput = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case ExecutorFailedEvent failed:
                    throw new InvalidOperationException(
                        $"Exception-handling executor '{failed.ExecutorId}' failed: {failed.Data}",
                        failed.Data as Exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("The exception-handling workflow failed.", error.Exception);
                case WorkflowOutputEvent when finalOutput is not null:
                    throw new InvalidOperationException(
                        "The exception-handling workflow produced multiple terminal results.");
                case WorkflowOutputEvent output:
                    finalOutput = output.As<string>();
                    break;
            }
        }

        return !string.IsNullOrWhiteSpace(finalOutput)
            ? finalOutput
            : throw new InvalidOperationException("The exception-handling workflow completed without a final response.");
    }

    private static AIAgent CreateAgent(IChatClient chatClient, string name, string instructions) =>
        new ChatClientAgent(chatClient, instructions, name: name)
            .AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, configure: options => options.EnableSensitiveData = true)
            .Build();

    private static async Task<string> RunHandlerAsync(AIAgent handler, string input, CancellationToken cancellationToken)
    {
        var response = await handler.RunAsync(input, cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(response.Text)
            ? throw new InvalidOperationException($"The {handler.Name} returned an empty response.")
            : response.Text;
    }

    // Only LocationLookupUnavailableException is recoverable; model/cancellation failures propagate.
    private sealed class PrimaryLookupExecutor(AIAgent primaryHandler, Action<string>? writeOutput)
        : Executor<ExceptionHandlingState, ExceptionHandlingState>("PrimaryLookup")
    {
        public override async ValueTask<ExceptionHandlingState> HandleAsync(
            ExceptionHandlingState state, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            if (state.PrimaryAttempts >= state.MaxPrimaryAttempts || state.PreciseResult is not null)
            {
                throw new InvalidOperationException("The primary lookup cannot run outside its retry budget.");
            }

            var attempt = state.PrimaryAttempts + 1;
            writeOutput?.Invoke($"\n--- Precise location lookup (attempt {attempt} of {state.MaxPrimaryAttempts}) ---");
            PreciseLocationInfo location;
            try
            {
                location = LocationLookupService.GetPreciseLocation(state.OriginalRequest, attempt);
            }
            catch (LocationLookupUnavailableException exception)
            {
                var next = state with { PrimaryAttempts = attempt, Failure = exception.Message };
                writeOutput?.Invoke($"Lookup error: {exception.Message}");
                writeOutput?.Invoke(
                    $"--- Primary attempt {attempt} failed; next: {(next.ShouldRetry ? "retry primary" : "area fallback")} ---");
                return next;
            }

            writeOutput?.Invoke($"Lookup result: {location.Address} ({location.Coordinates})");
            var explanation = await RunHandlerAsync(
                primaryHandler,
                $"Address: {location.Address}\nCoordinates: {location.Coordinates}\nSource: {location.Source}",
                cancellationToken);
            writeOutput?.Invoke($"--- Primary lookup succeeded on attempt {attempt} ---");
            return state with { PrimaryAttempts = attempt, PreciseResult = explanation, Failure = null };
        }
    }

    private sealed class FallbackLookupExecutor(AIAgent fallbackHandler, Action<string>? writeOutput)
        : Executor<ExceptionHandlingState, ExceptionHandlingState>("FallbackLookup")
    {
        public override async ValueTask<ExceptionHandlingState> HandleAsync(
            ExceptionHandlingState state, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            if (!state.ShouldFallback)
            {
                throw new InvalidOperationException("The fallback handler requires an exhausted primary lookup.");
            }

            var city = LocationLookupService.ExtractCity(state.Address);
            var area = LocationLookupService.GetGeneralArea(city);
            var sourceData = area is null
                ? $"City: {city}\nGeneral area result: No sample area record is available."
                : $"City: {area.City}\nSummary: {area.Summary}\nLandmark: {area.Landmark}";
            writeOutput?.Invoke("\n--- General-area fallback ---");
            var explanation = await RunHandlerAsync(
                fallbackHandler, $"Primary lookup failure: {state.Failure}\n{sourceData}", cancellationToken);
            writeOutput?.Invoke("--- Fallback result recorded ---");
            return state with { FallbackResult = explanation };
        }
    }

    // Deterministic final step: states which path produced the answer, so callers can tell precise from degraded.
    [YieldsOutput(typeof(string))]
    private sealed class ResponseExecutor(Action<string>? writeOutput) : Executor<ExceptionHandlingState>("Response")
    {
        public override async ValueTask HandleAsync(
            ExceptionHandlingState state, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            var response = state switch
            {
                { PreciseResult: { } precise } =>
                    $"Precise lookup succeeded after {state.PrimaryAttempts} attempt(s).\n{precise}",
                { FallbackResult: { } fallback } =>
                    $"Precise lookup failed after {state.PrimaryAttempts} attempt(s) ({state.Failure}); general-area fallback:\n{fallback}",
                _ => throw new InvalidOperationException("Recovery produced no result to present.")
            };
            writeOutput?.Invoke($"\n--- Final recovered response ---\n{response}");
            await context.YieldOutputAsync(response, cancellationToken);
        }
    }
}
