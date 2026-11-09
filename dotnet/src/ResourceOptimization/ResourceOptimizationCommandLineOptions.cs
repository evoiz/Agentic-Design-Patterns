namespace ResourceOptimization;

public sealed record ResourceOptimizationCommandLineOptions(string Mode, string? Deployment)
{
    public static ResourceOptimizationCommandLineOptions Parse(string[] args)
    {
        string? mode = null;
        string? deployment = null;
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
            {
                if (deployment is not null ||
                    index + 1 >= args.Length ||
                    string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new ArgumentException("Specify --deployment once with a non-empty name.", nameof(args));
                }

                deployment = args[++index];
            }
            else if (mode is null)
            {
                mode = args[index];
            }
            else
            {
                throw new ArgumentException("Specify only one demo variant.", nameof(args));
            }
        }

        mode ??= "complexity";
        if (!new[] { "all", "complexity", "research" }.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Unknown demo variant.", nameof(args));
        }

        return new(mode, deployment);
    }
}
