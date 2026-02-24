using System.Text.Json;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Obtiene snippets de resultados orgánicos de Google mediante SerpAPI (engine=google).
/// Usa el mismo patrón de proxy CORS que SerpApiImageSearchService para Blazor WebAssembly.
/// </summary>
public sealed class SerpApiOrganicSearchService : ISerpApiOrganicSearchService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(25);
    private static readonly HttpClient Client = new HttpClient { Timeout = RequestTimeout };

    private static readonly string[] CorsProxyBases = new[]
    {
        "https://corsproxy.io/?url=",
        "https://api.allorigins.win/raw?url="
    };

    private const string ApiBaseUrl = "https://serpapi.com/search.json";
    private const string DefaultGl = "ar";
    private const string DefaultHl = "es";

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetOrganicSnippetsAsync(
        string query,
        string apiKey,
        int maxSnippets = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(apiKey))
            return Array.Empty<string>();

        var q = query.Trim();
        var key = apiKey.Trim();
        var serpApiUrl = $"{ApiBaseUrl}?engine=google&q={Uri.EscapeDataString(q)}&api_key={Uri.EscapeDataString(key)}&gl={DefaultGl}&hl={DefaultHl}";

        try
        {
            string? json = null;
            Exception? lastEx = null;

            foreach (var proxyBase in CorsProxyBases)
            {
                try
                {
                    var proxyUrl = proxyBase + Uri.EscapeDataString(serpApiUrl);
                    using var req = new HttpRequestMessage(HttpMethod.Get, proxyUrl);
                    using var res = await Client.SendAsync(req, cancellationToken);
                    json = await res.Content.ReadAsStringAsync(cancellationToken);

                    if (proxyBase.Contains("allorigins", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                    {
                        try
                        {
                            using var wrap = JsonDocument.Parse(json);
                            if (wrap.RootElement.TryGetProperty("contents", out var contents))
                                json = contents.GetString() ?? json;
                        }
                        catch { }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    continue;
                }
            }

            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException(
                    "No se pudo conectar con SerpAPI (CORS o red). Probá de nuevo.",
                    lastEx);
            }

            return ParseOrganicSnippets(json, maxSnippets);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error al conectar con SerpAPI. " + ex.Message, ex);
        }
    }

    private static IReadOnlyList<string> ParseOrganicSnippets(string json, int maxSnippets)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errEl))
            {
                var msg = errEl.GetString();
                if (!string.IsNullOrWhiteSpace(msg))
                    throw new InvalidOperationException(msg);
            }

            if (!root.TryGetProperty("organic_results", out var organic) || organic.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var list = new List<string>();
            foreach (var item in organic.EnumerateArray())
            {
                if (list.Count >= maxSnippets) break;

                string? snippet = null;
                if (item.TryGetProperty("snippet", out var sn))
                    snippet = sn.GetString();
                if (string.IsNullOrWhiteSpace(snippet) && item.TryGetProperty("title", out var title))
                    snippet = title.GetString();

                if (!string.IsNullOrWhiteSpace(snippet))
                    list.Add(snippet.Trim());
            }
            return list;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }
}
