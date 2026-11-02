namespace Chapter14.RAG;

public sealed record RagCommandLineOptions(string Mode, string? DeploymentAlias, string Question)
{
    public static RagCommandLineOptions Parse(string[] args)
    {
        var validModes = new HashSet<string>(
            ["rag", "foundry-files", "foundry-web", "all"],
            StringComparer.OrdinalIgnoreCase);
        var mode = "rag";
        string? deploymentAlias = null;
        var questionParts = new List<string>();
        var modeSelected = false;

        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
            {
                if (deploymentAlias is not null ||
                    index + 1 >= args.Length ||
                    string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new ArgumentException("Specify --deployment once with a non-empty alias.", nameof(args));
                }

                deploymentAlias = args[++index];
            }
            else if (!modeSelected && questionParts.Count == 0 && validModes.Contains(args[index]))
            {
                mode = args[index];
                modeSelected = true;
            }
            else
            {
                questionParts.Add(args[index]);
            }
        }

        return new RagCommandLineOptions(
            mode,
            deploymentAlias,
            questionParts.Count == 0
                ? "What did the president say about Justice Breyer?"
                : string.Join(' ', questionParts));
    }
}
