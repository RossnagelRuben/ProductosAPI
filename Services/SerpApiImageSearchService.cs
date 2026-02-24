using System.Text.Json;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Implementación de búsqueda de imágenes con SerpAPI (motor Google Images).
/// Documentación: https://serpapi.com/google-images-api y https://serpapi.com/api-status-and-error-codes
/// Ubicación por defecto: Argentina (gl=ar, hl=es). Respeta SOLID: una sola razón de cambio (cambios en SerpAPI).
/// </summary>
/// <remarks>
/// Blazor WebAssembly corre en el navegador; las peticiones directas a serpapi.com son bloqueadas por CORS.
/// Por eso la petición se hace a través de un proxy CORS (corsproxy.io / allorigins.win), que hace el fetch
/// desde su servidor y devuelve la respuesta. La API key viaja en la URL al proxy; para producción idealmente
/// usar un backend propio que llame a SerpAPI desde el servidor.
/// </remarks>
public sealed class SerpApiImageSearchService : ISerpApiImageSearchService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly HttpClient Client = new HttpClient { Timeout = RequestTimeout };

    /// <summary>Proxies CORS para llamar a SerpAPI desde el navegador (evitar "Failed to fetch" por CORS). Se prueba el primero y luego el siguiente si falla.</summary>
    private static readonly string[] CorsProxyBases = new[]
    {
        "https://corsproxy.io/?url=",
        "https://api.allorigins.win/raw?url="
    };

    /// <summary>URL base de la API SerpAPI. Para imágenes: engine=google_images (ver https://serpapi.com/search-api).</summary>
    private const string ApiBaseUrl = "https://serpapi.com/search.json";

    /// <summary>Ubicación por defecto para resultados locales (Argentina).</summary>
    private const string DefaultLocation = "Argentina";

    /// <summary>Código de país Google para Argentina (parámetro gl). Da resultados coherentes con la ubicación.</summary>
    private const string DefaultGl = "ar";

    /// <summary>Idioma de la búsqueda (parámetro hl). Español para Argentina.</summary>
    private const string DefaultHl = "es";

    /// <summary>Máximo de URLs de imágenes a devolver por búsqueda (alineado con el flujo de la página).</summary>
    private const int MaxResults = 20;

    /// <inheritdoc />
    /// <remarks>
    /// En caso de error (API key inválida, sin créditos, o fallo de SerpAPI/Google), se lanza una excepción
    /// con el mensaje devuelto por la API (campo "error" del JSON o cuerpo de la respuesta) para que la UI lo muestre.
    /// </remarks>
    public async Task<IReadOnlyList<string>> SearchImageUrlsAsync(
        string query,
        string apiKey,
        string? location = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(apiKey))
            return Array.Empty<string>();

        var loc = string.IsNullOrWhiteSpace(location) ? DefaultLocation : location.Trim();
        var q = query.Trim();
        var key = apiKey.Trim();

        // Parámetros según documentación: q, api_key, engine=google_images, location; gl y hl para localización consistente (Argentina).
        var serpApiUrl = $"{ApiBaseUrl}?engine=google_images&q={Uri.EscapeDataString(q)}&api_key={Uri.EscapeDataString(key)}&location={Uri.EscapeDataString(loc)}&gl={DefaultGl}&hl={DefaultHl}";

        try
        {
            // Llamar a SerpAPI a través de un proxy CORS para evitar "TypeError: Failed to fetch" en Blazor WebAssembly (el navegador bloquea peticiones directas a serpapi.com).
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

                    // allorigins.win con /raw devuelve el cuerpo tal cual; otros endpoints pueden envolver en { "contents": "..." }. Extraer por si acaso.
                    if (proxyBase.Contains("allorigins", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("{", StringComparison.Ordinal))
                    {
                        try
                        {
                            using var wrap = JsonDocument.Parse(json);
                            if (wrap.RootElement.TryGetProperty("contents", out var contents))
                                json = contents.GetString() ?? json;
                        }
                        catch { /* no es wrapper, usar json tal cual */ }
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
                    "No se pudo conectar con SerpAPI (CORS o red). Probá de nuevo o revisá que los proxies CORS estén disponibles.",
                    lastEx);
            }

            // El proxy suele devolver 200 aunque SerpAPI haya devuelto error; los errores de SerpAPI vienen en el JSON (campo "error" o search_metadata.status). ParseImageUrlsAndHandleErrors los procesa.
            return ParseImageUrlsAndHandleErrors(json);
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

    /// <summary>Extrae el mensaje del campo "error" del JSON de respuesta de SerpAPI (cuando la petición falla o no hay resultados).</summary>
    private static string? TryGetErrorFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var errEl))
                return errEl.GetString();
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Parsea el JSON de respuesta de SerpAPI, comprueba search_metadata.status y el campo "error",
    /// y extrae URLs del array "images_results". Si el estado es Error o hay mensaje de error sin imágenes, lanza con ese mensaje.
    /// Prioriza "original" (alta resolución) y usa "thumbnail" como fallback. Excluye PDFs.
    /// </summary>
    private static IReadOnlyList<string> ParseImageUrlsAndHandleErrors(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("SerpAPI no devolvió datos.");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // search_metadata.status: "Processing" -> "Success" o "Error". Ver https://serpapi.com/api-status-and-error-codes
        if (root.TryGetProperty("search_metadata", out var meta) && meta.TryGetProperty("status", out var statusEl))
        {
            var status = statusEl.GetString();
            if (string.Equals(status, "Error", StringComparison.OrdinalIgnoreCase))
            {
                var msg = TryGetErrorFromJson(json) ?? "La búsqueda en SerpAPI falló. Probá de nuevo más tarde.";
                throw new InvalidOperationException(msg);
            }
        }

        // SerpAPI devuelve el array "images_results" (documentación: Google Images Results API).
        if (!root.TryGetProperty("images_results", out var imagesResults) || imagesResults.ValueKind != JsonValueKind.Array)
        {
            var apiMsg = TryGetErrorFromJson(json);
            if (!string.IsNullOrWhiteSpace(apiMsg))
                throw new InvalidOperationException(apiMsg);
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var item in imagesResults.EnumerateArray())
        {
            if (result.Count >= MaxResults) break;

            // Preferir URL original (alta resolución); si no existe, usar thumbnail (documentación: original, thumbnail).
            string? url = null;
            if (item.TryGetProperty("original", out var origEl))
                url = origEl.GetString();
            if (string.IsNullOrWhiteSpace(url) && item.TryGetProperty("thumbnail", out var thumbEl))
                url = thumbEl.GetString();

            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!IsHttpOrHttps(url)) continue;
            if (EsPdfPorUrl(url)) continue;

            result.Add(url);
        }

        // Si no se extrajo ninguna URL pero la API incluyó un mensaje (p. ej. "Google hasn't returned any results for this query"), mostrarlo.
        if (result.Count == 0)
        {
            var apiMsg = TryGetErrorFromJson(json);
            if (!string.IsNullOrWhiteSpace(apiMsg))
                throw new InvalidOperationException(apiMsg);
        }

        return result;
    }

    /// <summary>Excluye PDFs por URL (no descargar ni mostrar como imagen).</summary>
    private static bool EsPdfPorUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.IndexOf(".pdf", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        try
        {
            var decoded = Uri.UnescapeDataString(url);
            if (decoded != url && decoded.IndexOf(".pdf", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        catch { }
        return false;
    }

    /// <summary>Solo URLs http/https para que el navegador o proxy puedan cargarlas.</summary>
    private static bool IsHttpOrHttps(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
