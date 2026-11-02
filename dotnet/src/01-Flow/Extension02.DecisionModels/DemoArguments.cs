namespace Extension02.DecisionModels;

public sealed record DemoArguments(string Variant, string Provider, string? Input)
{
    public static DemoArguments Parse(string[] args)
    {
        var hasVariant = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal);
        var variant = hasVariant ? args[0] : "all";
        if (variant is not ("all" or "microsoft" or "openai" or "maf"))
        {
            throw new ArgumentException("Variant must be all, microsoft, openai, or maf. Use --help.");
        }
        string? input = null;
        var provider = "microsoft";
        for (var i = hasVariant ? 1 : 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {args[i]}.");
            }
            switch (args[i])
            {
                case "--input": input = args[i + 1]; ArgumentException.ThrowIfNullOrWhiteSpace(input); break;
                case "--provider": provider = args[i + 1]; break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (provider is not ("microsoft" or "openai"))
        {
            throw new ArgumentException("Provider must be microsoft or openai.");
        }
        return new DemoArguments(variant, provider, input);
    }
}
