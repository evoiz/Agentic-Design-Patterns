namespace Chapter19.Evaluation;

public static class EvaluationSamples
{
    public const string CapitalQuery = "What is the capital of France?";
    public const string CapitalResponse = "The capital of France is Paris.";
    public const string CapitalReference = "Paris is the capital of France.";

    public static IReadOnlyList<(string Label, string Question)> SurveyQuestions { get; } =
    [
        ("Good (notebook label, not ground truth)", """
            To what extent do you agree or disagree that current intellectual property laws in Switzerland adequately protect emerging AI-generated content, assuming the content meets the originality criteria established by the Federal Supreme Court?
            (Select one: Strongly Disagree, Disagree, Neutral, Agree, Strongly Agree)
            """),
        ("Biased", """
            Don't you agree that overly restrictive data privacy laws like the FADP are hindering essential technological innovation and economic growth in Switzerland?
            (Select one: Yes, No)
            """),
        ("Vague", "What are your thoughts on legal tech?")
    ];
}
