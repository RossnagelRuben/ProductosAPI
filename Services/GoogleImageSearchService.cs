using System.Text.Json;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>Implementación de búsqueda de imágenes con Google Custom Search JSON API (README GOOGLE BUSQUEDAS.md).</summary>
public sealed class GoogleImageSearchService : IGoogleImageSearchService
{
    /// <summary>Timeout corto para que la descarga no tarde: falla rápido y se puede probar otra imagen.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly HttpClient Client = new HttpClient { Timeout = RequestTimeout };
    private const string ApiUrl = "https://www.googleapis.com/customsearch/v1";
    private const string DefaultCx = "012225991981024570250:pketlhy4f0h";
    /// <summary>Límite razonable para no convertir imágenes enormes a base64 y colgar/consumir toda la memoria (sobre todo en GitHub Pages).</summary>
    private const int MaxImageBytes = 4 * 1024 * 1024; // 4 MB

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
        if (EsPdfPorUrl(imageUrl)) return null;
        try
        {
            using var res = await Client.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!res.IsSuccessStatusCode) return null;
            var contentType = res.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (EsPdfMime(contentType)) return null;
            var contentLength = res.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > MaxImageBytes) return null;
            var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxImageBytes) return null;
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) contentType = "image/jpeg";
            return "data:" + contentType + ";base64," + Convert.ToBase64String(bytes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static bool EsPdfMime(string? mime)
    {
        return !string.IsNullOrWhiteSpace(mime) && mime.Trim().StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase);
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
                string? mime = null;
                if (item.TryGetProperty("mime", out var mimeEl))
                    mime = mimeEl.GetString();
                if (EsPdfMime(mime)) continue;
                if (!string.IsNullOrWhiteSpace(mime) && !mime.TrimStart().StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (item.TryGetProperty("link", out var linkEl))
                {
                    var link = linkEl.GetString();
                    if (string.IsNullOrWhiteSpace(link) || !IsHttpOrHttps(link)) continue;
                    if (EsPdfPorUrl(link)) continue;
                    result.Add(link);
                }
                if (result.Count >= 10) break;
            }
        }
        catch { }
        return result;
    }

    /// <summary>Excluye PDFs por URL (no traer ni descargar PDF). Revisa URL cruda y decodificada por si viene en redirects de Google.</summary>
    private static bool EsPdfPorUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (ContieneIndicioPdf(url)) return true;
        try
        {
            var decoded = Uri.UnescapeDataString(url);
            if (decoded != url && ContieneIndicioPdf(decoded)) return true;
        }
        catch { }
        return false;
    }

    private static bool ContieneIndicioPdf(string url)
    {
        if (url.IndexOf(".pdf", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (url.IndexOf("application/pdf", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (url.IndexOf("application%2Fpdf", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        var q = url.IndexOf('?');
        var path = q >= 0 ? url.Substring(0, q) : url;
        if (path.EndsWith("/pdf", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Length >= 4 && path[path.Length - 4] == '.' && path.EndsWith("pdf", StringComparison.OrdinalIgnoreCase)) return true;
        if (q >= 0 && q < url.Length - 1)
        {
            var query = url.Substring(q + 1);
            if (query.IndexOf("format=pdf", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (query.IndexOf("type=pdf", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    /// <summary>Solo incluimos URLs que el navegador o un proxy puedan cargar (http/https). Descartamos x-raw-image://, data:, etc.</summary>
    private static bool IsHttpOrHttps(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
