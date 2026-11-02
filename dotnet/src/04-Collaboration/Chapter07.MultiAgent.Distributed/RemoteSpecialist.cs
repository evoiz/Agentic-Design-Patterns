using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;

namespace Chapter07.MultiAgent.Distributed;

public sealed class RemoteSpecialist
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly TokenCredential _credential;

    public RemoteSpecialist(HttpClient httpClient, string endpoint, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(credential);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !(uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp ||
              uri.Scheme == Uri.UriSchemeHttps &&
              uri.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Specialist endpoint must be a Foundry HTTPS Responses URL or a loopback HTTP URL for local testing.",
                nameof(endpoint));
        }

        if (!uri.AbsolutePath.EndsWith("/responses", StringComparison.Ordinal))
        {
            throw new ArgumentException("Provide the full specialist /responses endpoint.", nameof(endpoint));
        }

        _httpClient = httpClient;
        _endpoint = uri;
        _credential = credential;
    }

    public async Task<string> InvokeAsync(string input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = JsonContent.Create(new { input, stream = false })
        };
        if (!_endpoint.IsLoopback)
        {
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(["https://ai.azure.com/.default"]),
                cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        // Do not convert failed specialist calls into a fabricated article or retry side effects.
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("status", out var status) || status.GetString() != "completed")
        {
            throw new InvalidOperationException(
                $"Specialist '{_endpoint.Host}' did not complete its response.");
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Specialist response contains no output array.");
        }

        var text = new List<string>();
        foreach (var item in output.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var itemType) && itemType.GetString() == "message" &&
                item.TryGetProperty("role", out var role) && role.GetString() == "assistant" &&
                item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var partType) && partType.GetString() == "output_text" &&
                        part.TryGetProperty("text", out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        text.Add(value.GetString()!);
                    }
                }
            }
        }

        return text.Count > 0
            ? string.Join("\n", text)
            : throw new InvalidOperationException("Specialist completed without assistant output text.");
    }
}
