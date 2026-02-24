using System.Text.Json;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>Implementación de búsqueda de imágenes con Google Custom Search JSON API (README GOOGLE BUSQUEDAS.md).</summary>
public sealed class GoogleImageSearchService : IGoogleImageSearchService
{
    private static readonly HttpClient Client = new HttpClient();
    private const string ApiUrl = "https://www.googleapis.com/customsearch/v1";
    private const string DefaultCx = "012225991981024570250:pketlhy4f0h";

    public async Task<IReadOnlyList<string>> SearchImageUrlsAsync(string query, IReadOnlyList<string> apiKeys, string? searchEngineId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || apiKeys == null || apiKeys.Count == 0)
            return Array.Empty<string>();

        var cx = string.IsNullOrWhiteSpace(searchEngineId) ? DefaultCx : searchEngineId.Trim();
        var q = query.Trim();
        var urls = new List<string>();

        foreach (var key in apiKeys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var keyTrim = key.Trim();
            var url = $"{ApiUrl}?key={Uri.EscapeDataString(keyTrim)}&cx={Uri.EscapeDataString(cx)}&q={Uri.EscapeDataString(q)}&searchType=image&fileType=jpeg,png";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                using var res = await Client.SendAsync(req, cancellationToken);
                if (!res.IsSuccessStatusCode)
                    continue;
                var json = await res.Content.ReadAsStringAsync(cancellationToken);
                var list = ParseImageLinks(json);
                if (list.Count > 0)
                    return list;
            }
            catch
            {
                // rotar a la siguiente key
            }
        }

        return urls;
    }

    public async Task<string?> FetchImageAsDataUrlAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || (!imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            return null;
        try
        {
            using var res = await Client.GetAsync(imageUrl, cancellationToken);
            if (!res.IsSuccessStatusCode) return null;
            var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes == null || bytes.Length == 0) return null;
            var contentType = res.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) contentType = "image/jpeg";
            return "data:" + contentType + ";base64," + Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ParseImageLinks(string json)
    {
        var result = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return result;
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("link", out var linkEl))
                {
                    var link = linkEl.GetString();
                    if (!string.IsNullOrWhiteSpace(link) && !link.Contains(".pdf", StringComparison.OrdinalIgnoreCase) && IsHttpOrHttps(link))
                        result.Add(link);
                }
                if (result.Count >= 10) break;
            }
        }
        catch { }
        return result;
    }

    /// <summary>Solo incluimos URLs que el navegador o un proxy puedan cargar (http/https). Descartamos x-raw-image://, data:, etc.</summary>
    private static bool IsHttpOrHttps(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
