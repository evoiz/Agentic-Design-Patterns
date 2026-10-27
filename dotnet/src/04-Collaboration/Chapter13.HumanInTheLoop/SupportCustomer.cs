namespace Chapter13.HumanInTheLoop;

public sealed record SupportCustomer
{
    public string Name { get; init; } = "valued customer";
    public string Tier { get; init; } = "standard";
    public List<string> RecentPurchases { get; init; } = [];
    public List<string> SupportHistory { get; init; } = [];

    public void Validate()
    {
        ValidateText(Name, nameof(Name), 120);
        ValidateText(Tier, nameof(Tier), 80);
        ValidateList(RecentPurchases, nameof(RecentPurchases));
        ValidateList(SupportHistory, nameof(SupportHistory));
    }

    private static void ValidateList(IReadOnlyCollection<string>? values, string name)
    {
        if (values is null)
        {
            throw new ArgumentException($"{name} cannot be null.");
        }

        if (values.Count > 10)
        {
            throw new ArgumentException($"{name} cannot contain more than 10 entries.");
        }

        foreach (var value in values)
        {
            ValidateText(value, name, 200);
        }
    }

    private static void ValidateText(string value, string name, int maximumLength)
    {
        if (value is null)
        {
            throw new ArgumentException($"{name} cannot be null.");
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentException($"{name} cannot exceed {maximumLength} characters.");
        }
    }
}
