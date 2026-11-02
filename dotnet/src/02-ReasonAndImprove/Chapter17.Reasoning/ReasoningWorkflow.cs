using Chapter17.Reasoning.Models;
using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter17.Reasoning;

public static class ReasoningWorkflow
{
    public const string TelemetrySourceName = "Chapter17.Reasoning";
    public const int MaxCorrectionRounds = 3;
    public const int MaxResearchRounds = 2;
    public const int MaxQueriesPerRound = 3;

    public static Task<string> RunAsync(
        IChatClient chatClient,
        ReasoningScenario scenario,
        string input,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        cancellationToken.ThrowIfCancellationRequested();

        return scenario switch
        {
            ReasoningScenario.ChainOfThought =>
                RunReasonedAnswerAsync(chatClient, input, writeOutput, cancellationToken),
            ReasoningScenario.SelfCorrection =>
                RunSelfCorrectionAsync(chatClient, input, writeOutput, cancellationToken),
            ReasoningScenario.ReAct =>
                RunReActAsync(chatClient, input, writeOutput, cancellationToken),
            ReasoningScenario.DeepSearch =>
                RunDeepSearchAsync(chatClient, input, writeOutput, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown reasoning scenario.")
        };
    }

    public static string CalculateFactorial(int value)
    {
        if (value is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Factorial supports integers from 0 through 20.");
        }

        long result = 1;
        for (var factor = 2; factor <= value; factor++)
        {
            result *= factor;
        }

        return result.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> RunReasonedAnswerAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var agent = CreateAgent(
            chatClient,
            "ReasoningAgent",
            """
            Solve the user's problem carefully before answering. Do not reveal private chain-of-thought or hidden
            step-by-step reasoning. Return a direct answer followed by a concise, user-facing rationale that
            summarizes the key factors without exposing internal deliberation. Qualify uncertainty and avoid
            unsupported claims.
            """);
        var response = await agent.RunAsync(input, cancellationToken: cancellationToken);
        var answer = RequireText(response.Text, "The reasoning agent returned an empty answer.");
        writeOutput?.Invoke("\n--- Answer with concise rationale ---");
        writeOutput?.Invoke(answer);
        return answer;
    }

    private static async Task<string> RunSelfCorrectionAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var generator = CreateAgent(
            chatClient,
            "DraftWriter",
            """
            Follow the user's original requirements to write or revise the requested draft. Preserve the requested
            subject, product or organization names, facts, and tone. The result must not exceed 150 characters,
            regardless of any conflicting length request. Output only the draft and do not invent unsupported claims.
            """);
        var reviewer = CreateAgent(
            chatClient,
            "IndependentReviewer",
            """
            Review the draft against the user's original requirements. Check that it preserves the requested subject,
            names, facts, and tone, and that it does not exceed 150 characters. The 150-character limit is mandatory
            even if the user requests a longer draft. Accept only when the requirements and limit are met. Return
            structured output with Accept set to true or false and Feedback containing a concise explanation or
            actionable fixes. Do not invent unsupported claims.
            """);
        var draft = string.Empty;
        var feedback = string.Empty;
        var accepted = false;
        var rounds = 0;

        for (var round = 1; round <= MaxCorrectionRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prompt = round == 1
                ? input
                : $"""
                  Original requirements:
                  {input}

                  Previous draft:
                  {draft}

                  Reviewer feedback:
                  {feedback}

                  Revise the post to meet the requirements. Return only the complete post.
                  """;
            draft = RequireText(
                (await generator.RunAsync(prompt, cancellationToken: cancellationToken)).Text,
                "The self-correction generator returned an empty draft.");
            rounds = round;
            writeOutput?.Invoke($"\n--- Draft ({round} of {MaxCorrectionRounds}) ---");
            writeOutput?.Invoke(draft);

            var review = (await reviewer.RunAsync<ReasoningReview>(
                $"Original requirements:\n{input}\n\nDraft:\n{draft}",
                cancellationToken: cancellationToken)).Result;
            if (review is null || string.IsNullOrWhiteSpace(review.Feedback))
            {
                throw new InvalidOperationException("The self-correction reviewer returned an incomplete review.");
            }

            var characterCount = new StringInfo(draft).LengthInTextElements;
            feedback = characterCount <= 150
                ? review.Feedback
                : $"{review.Feedback} The draft has {characterCount} characters; reduce it to 150 or fewer.";
            accepted = review.Accept && characterCount <= 150;
            writeOutput?.Invoke("\n--- Reviewer feedback ---");
            writeOutput?.Invoke(feedback);
            if (accepted)
            {
                break;
            }
        }

        var result = $"""
            Outcome: {(accepted ? "Accepted" : "MaxRoundsReached")}
            Rounds: {rounds} of {MaxCorrectionRounds}
            {(accepted ? "The reviewer accepted this draft." : "The draft was not accepted within the round limit.")}

            Final draft:
            {draft}

            Review:
            {feedback}
            """;
        writeOutput?.Invoke("\n--- Self-correction result ---");
        writeOutput?.Invoke(result);
        return result;
    }

    private static async Task<string> RunReActAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        string CalculateAndReport(int value)
        {
            var result = CalculateFactorial(value);
            writeOutput?.Invoke($"\n--- Tool call: calculate_factorial({value}) ---\nResult: {result}");
            return result;
        }

        var factorialTool = AIFunctionFactory.Create(
            (Func<int, string>)CalculateAndReport,
            name: "calculate_factorial",
            description: "Calculates an exact factorial for an integer from 0 through 20.");
        var agent = CreateAgent(
            chatClient,
            "FactorialReasoningAgent",
            """
            Answer factorial questions by calling calculate_factorial before giving the result.
            Do not calculate the value yourself or claim to execute general-purpose code.
            Explain the result briefly and accurately.
            """,
            factorialTool);
        var response = await agent.RunAsync(input, cancellationToken: cancellationToken);
        var answer = RequireText(response.Text, "The ReAct agent returned an empty answer.");
        writeOutput?.Invoke("\n--- ReAct result ---");
        writeOutput?.Invoke(answer);
        return answer;
    }

    private static async Task<string> RunDeepSearchAsync(
        IChatClient chatClient,
        string input,
        Action<string>? writeOutput,
        CancellationToken cancellationToken)
    {
        var planner = CreateAgent(
            chatClient,
            "QueryPlanner",
            $"""
            Decompose the user's question into 1 to {MaxQueriesPerRound} focused research queries.
            Return structured output with a Queries string array. Make queries specific and nonredundant.
            This demo has no live web-search tool; the queries guide model-knowledge research only.
            """);
        var plan = (await planner.RunAsync<ResearchPlan>(input, cancellationToken: cancellationToken)).Result;
        var queries = RequireQueries(plan?.Queries, "The research planner returned no valid queries.");
        var researcher = CreateAgent(
            chatClient,
            "KnowledgeResearcher",
            """
            Provide concise background relevant to one research query using model knowledge only.
            Do not claim to browse, cite sources, or verify current information. State uncertainty and knowledge limits.
            """);
        var reflector = CreateAgent(
            chatClient,
            "ResearchReflector",
            $"""
            Assess whether the supplied notes address the user's question. Return structured output with
            SufficientCoverage, a concise Summary, and FollowUpQueries. If more information would materially
            improve the answer, provide at most {MaxQueriesPerRound} focused follow-up queries.
            The notes are model knowledge, not verified web research.
            """);
        var notes = new List<string>();
        ResearchAssessment? assessment = null;

        for (var round = 1; round <= MaxResearchRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writeOutput?.Invoke($"\n--- Research queries (round {round} of {MaxResearchRounds}) ---");
            foreach (var query in queries)
            {
                writeOutput?.Invoke($"Query: {query}");
                var response = await researcher.RunAsync(query, cancellationToken: cancellationToken);
                var note = RequireText(response.Text, "The model-knowledge researcher returned an empty note.");
                notes.Add($"Query: {query}\nModel-knowledge note: {note}");
                writeOutput?.Invoke(note);
            }

            assessment = (await reflector.RunAsync<ResearchAssessment>(
                $"Question:\n{input}\n\nResearch notes:\n{string.Join("\n\n", notes)}",
                cancellationToken: cancellationToken)).Result;
            if (assessment is null || string.IsNullOrWhiteSpace(assessment.Summary))
            {
                throw new InvalidOperationException("The research reflector returned an incomplete assessment.");
            }

            writeOutput?.Invoke("\n--- Research reflection ---");
            writeOutput?.Invoke(assessment.Summary);
            if (assessment.SufficientCoverage || round == MaxResearchRounds)
            {
                break;
            }

            queries = RequireQueries(
                assessment.FollowUpQueries,
                "The research reflector requested more coverage without providing follow-up queries.");
        }

        var synthesizer = CreateAgent(
            chatClient,
            "AnswerSynthesizer",
            """
            Synthesize a clear answer to the original question from the supplied notes and coverage assessment.
            Distinguish model-knowledge background from verified research, disclose important gaps, and do not
            invent citations or claim live web access.
            """);
        var finalAssessment = assessment
            ?? throw new InvalidOperationException("The research flow completed without a coverage assessment.");
        var finalResponse = await synthesizer.RunAsync(
            $"Question:\n{input}\n\nResearch notes:\n{string.Join("\n\n", notes)}\n\nCoverage assessment:\n{finalAssessment.Summary}",
            cancellationToken: cancellationToken);
        var answer = RequireText(finalResponse.Text, "The research synthesizer returned an empty answer.");
        writeOutput?.Invoke("\n--- Synthesized answer (not live web research) ---");
        writeOutput?.Invoke(answer);
        return answer;
    }

    private static AIAgent CreateAgent(
        IChatClient chatClient,
        string name,
        string instructions,
        params AITool[] tools) =>
        new ChatClientAgent(
                chatClient,
                instructions,
                name: name,
                tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(
                TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();

    private static string[] RequireQueries(IEnumerable<string>? candidate, string errorMessage)
    {
        if (candidate is null)
        {
            throw new InvalidOperationException(errorMessage);
        }

        var queries = candidate.Select(query => query?.Trim()).ToArray();
        if (queries.Length is < 1 or > MaxQueriesPerRound ||
            queries.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"{errorMessage} Supply between one and {MaxQueriesPerRound} nonempty queries.");
        }

        return queries!;
    }

    private static string RequireText(string? value, string errorMessage) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(errorMessage);
}
