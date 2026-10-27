using System.Text.Json;
using Chapter16.ResourceOptimization.Models;
using Microsoft.Extensions.Configuration;

namespace Chapter16.ResourceOptimization;

public interface IGoogleCustomSearch
{
    Task<IReadOnlyList<GoogleSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}

public sealed class GoogleCustomSearchClient : IGoogleCustomSearch, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _searchEngineId;
    private readonly bool _ownsHttpClient;

    public GoogleCustomSearchClient(string apiKey, string searchEngineId, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchEngineId);
        _apiKey = apiKey;
        _searchEngineId = searchEngineId;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public static GoogleCustomSearchClient? FromConfiguration(IConfiguration configuration)
    {
        var apiKey = configuration["GoogleCustomSearch:ApiKey"]
            ?? configuration["GOOGLE_CUSTOM_SEARCH_API_KEY"]
            ?? configuration["CHAPTER16_GOOGLE_CUSTOM_SEARCH_API_KEY"];
        var searchEngineId = configuration["GoogleCustomSearch:SearchEngineId"]
            ?? configuration["GOOGLE_CSE_ID"]
            ?? configuration["CHAPTER16_GOOGLE_CSE_ID"];

        if (string.IsNullOrWhiteSpace(apiKey) && string.IsNullOrWhiteSpace(searchEngineId))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(searchEngineId))
        {
            throw new InvalidOperationException(
                "Configure both GoogleCustomSearch:ApiKey and GoogleCustomSearch:SearchEngineId, " +
                "or leave both unset.");
        }

        return new GoogleCustomSearchClient(apiKey, searchEngineId);
    }

    public async Task<IReadOnlyList<GoogleSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var uri = new Uri(
            "https://www.googleapis.com/customsearch/v1" +
            $"?key={Uri.EscapeDataString(_apiKey)}" +
            $"&cx={Uri.EscapeDataString(_searchEngineId)}" +
            $"&q={Uri.EscapeDataString(query)}&num=3");

        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(item => new GoogleSearchResult(
                ReadProperty(item, "title"),
                ReadProperty(item, "snippet"),
                ReadProperty(item, "link")))
            .ToArray();
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string ReadProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
}
