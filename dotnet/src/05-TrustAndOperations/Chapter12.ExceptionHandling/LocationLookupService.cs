namespace Chapter12.ExceptionHandling;

public sealed record PreciseLocationInfo(string Address, string Coordinates, string Source);

public sealed record GeneralAreaInfo(string City, string Summary, string Landmark);

public sealed class LocationLookupUnavailableException(string message) : Exception(message);

// Deterministic stand-in for a geocoding provider. Prefixes simulate failures:
// "transient:" fails attempt 1 only; "primary-unavailable:" fails every attempt.
public static class LocationLookupService
{
    private const string TransientPrefix = "transient:";
    private const string UnavailablePrefix = "primary-unavailable:";

    private static readonly Dictionary<string, string> Coordinates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1600 Amphitheatre Parkway, Mountain View, CA"] = "37.4220, -122.0841",
        ["1 Market Street, San Francisco, CA"] = "37.7936, -122.3958"
    };

    private static readonly Dictionary<string, GeneralAreaInfo> GeneralAreas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mountain View"] = new("Mountain View", "A city in California's Silicon Valley.", "Shoreline Amphitheatre"),
        ["San Francisco"] = new("San Francisco", "A coastal city in Northern California.", "The Ferry Building")
    };

    public static string ParseAddress(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var address = request.Trim();
        foreach (var prefix in new[] { TransientPrefix, UnavailablePrefix })
        {
            if (address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                address = address[prefix.Length..].Trim();
            }
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return address;
    }

    public static PreciseLocationInfo GetPreciseLocation(string request, int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempt);
        var address = ParseAddress(request);
        var normalized = request.Trim();
        if (normalized.StartsWith(UnavailablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocationLookupUnavailableException("The precise-location provider is unavailable for this request.");
        }

        if (attempt == 1 && normalized.StartsWith(TransientPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocationLookupUnavailableException("The precise-location provider returned a transient failure.");
        }

        return Coordinates.TryGetValue(address, out var coordinates)
            ? new PreciseLocationInfo(address, coordinates, "Sample precise-location index")
            : throw new LocationLookupUnavailableException($"No precise-location record was found for '{address}'.");
    }

    public static GeneralAreaInfo? GetGeneralArea(string city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        return GeneralAreas.GetValueOrDefault(city.Trim());
    }

    // Prefer a known city component; otherwise assume "street, city, region".
    public static string ExtractCity(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault(GeneralAreas.ContainsKey) ?? (parts.Length >= 2 ? parts[^2] : parts[0]);
    }
}
