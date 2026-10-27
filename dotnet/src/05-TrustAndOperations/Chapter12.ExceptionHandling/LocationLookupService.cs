namespace Chapter12.ExceptionHandling;

public sealed record PreciseLocationInfo(string Address, string Coordinates, string Source);

public sealed record GeneralAreaInfo(string City, string Summary, string Landmark);

public sealed class LocationLookupUnavailableException(string message) : Exception(message);

public static class LocationLookupService
{
    private const string TransientPrefix = "transient:";
    private const string UnavailablePrefix = "primary-unavailable:";

    private static readonly IReadOnlyDictionary<string, PreciseLocationInfo> PreciseLocations =
        new Dictionary<string, PreciseLocationInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["1600 Amphitheatre Parkway, Mountain View, CA"] =
                new("1600 Amphitheatre Parkway, Mountain View, CA", "37.4220, -122.0841", "Sample precise-location index"),
            ["1 Market Street, San Francisco, CA"] =
                new("1 Market Street, San Francisco, CA", "37.7936, -122.3958", "Sample precise-location index")
        };

    private static readonly IReadOnlyDictionary<string, GeneralAreaInfo> GeneralAreas =
        new Dictionary<string, GeneralAreaInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mountain View"] = new(
                "Mountain View",
                "A city in California's Silicon Valley.",
                "Shoreline Amphitheatre"),
            ["San Francisco"] = new(
                "San Francisco",
                "A coastal city in Northern California.",
                "The Ferry Building")
        };

    public static string ParseAddress(string request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        var address = request.Trim();
        if (address.StartsWith(TransientPrefix, StringComparison.OrdinalIgnoreCase))
        {
            address = address[TransientPrefix.Length..].Trim();
        }
        else if (address.StartsWith(UnavailablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            address = address[UnavailablePrefix.Length..].Trim();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return address;
    }

    public static PreciseLocationInfo GetPreciseLocation(string request, int attempt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempt);

        var normalizedRequest = request.Trim();
        var simulatesTransientFailure =
            normalizedRequest.StartsWith(TransientPrefix, StringComparison.OrdinalIgnoreCase);
        var simulatesUnavailableService =
            normalizedRequest.StartsWith(UnavailablePrefix, StringComparison.OrdinalIgnoreCase);
        var address = ParseAddress(normalizedRequest);

        if (simulatesUnavailableService ||
            (simulatesTransientFailure && attempt == 1) ||
            !PreciseLocations.TryGetValue(address, out var result))
        {
            throw new LocationLookupUnavailableException(
                simulatesUnavailableService
                    ? "The precise-location provider is unavailable for this request."
                    : simulatesTransientFailure && attempt == 1
                        ? "The precise-location provider returned a transient failure."
                        : $"No precise-location record was found for '{address}'.");
        }

        return result;
    }

    public static GeneralAreaInfo? GetGeneralArea(string city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        return GeneralAreas.TryGetValue(city.Trim(), out var result) ? result : null;
    }

    public static string ExtractCity(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var supportedCity = GeneralAreas.Keys.FirstOrDefault(city =>
            parts.Contains(city, StringComparer.OrdinalIgnoreCase));
        if (supportedCity is not null)
        {
            return supportedCity;
        }

        if (parts.Length >= 2)
        {
            return parts[^2];
        }

        return parts[0];
    }
}
