using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.JSInterop;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Implementación de <see cref="IIntegrationImageSearchService"/> contra la API DRR (/Integration/ImageSearch).
/// Sigue SOLID: una única razón de cambio (contrato de la API de integración).
/// </summary>
public sealed class IntegrationImageSearchService : IIntegrationImageSearchService
{
    private const string ApiUrl = "https://drrsystemas4.azurewebsites.net/Integration/ImageSearch";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;

    public IntegrationImageSearchService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<IReadOnlyList<string>> SearchImageUrlsAsync(
        string query,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(token))
            return Array.Empty<string>();

        var q = query.Trim();
        var bearer = token.Trim();

        var url = $"{ApiUrl}?query={Uri.EscapeDataString(q)}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        using var resp = await _httpClient.SendAsync(req, cancellationToken);
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);

        // Log diagnóstico en consola para inspeccionar el formato real que devuelve la API.
        try
        {
            var preview = json.Length > 2000 ? json.Substring(0, 2000) + "…" : json;
            var payload = new
            {
                tag = "IntegrationImageSearch RESPONSE",
                requestUrl = url,
                statusCode = (int)resp.StatusCode,
                status = resp.StatusCode.ToString(),
                contentLength = json.Length,
                contentPreview = preview
            };
            await _jsRuntime.InvokeVoidAsync("__logAsignarImagenes", "IntegrationImageSearch", JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch
        {
            // No romper si el log falla.
        }

        if (!resp.IsSuccessStatusCode)
        {
            var message = string.IsNullOrWhiteSpace(json)
                ? $"La API de integración devolvió HTTP {(int)resp.StatusCode} ({resp.StatusCode})."
                : $"La API de integración devolvió HTTP {(int)resp.StatusCode} ({resp.StatusCode}): {json}";
            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("La API de integración no devolvió contenido.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Si la API devuelve explícitamente status:"error", usamos el mensaje y no intentamos parsear más.
            if (root.TryGetProperty("status", out var statusProp))
            {
                var statusText = statusProp.GetString();
                if (!string.IsNullOrWhiteSpace(statusText) &&
                    statusText.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    string? msg = null;
                    if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                        msg = msgProp.GetString();
                    throw new InvalidOperationException(msg ?? "La búsqueda no obtuvo resultados.");
                }
            }

            // Formatos soportados:
            // 1) { status: "ok", data: { items: [ { imageUrl: "...", ... }, ... ] } }
            // 2) { data: [ "url1", "url2", ... ] }
            // 3) { data: [ { url: "..." }, { imageUrl: "..." }, ... ] }
            // 4) [ "url1", "url2", ... ]
            // 5) [ { url: "..." }, ... ]
            JsonElement? arrayNode = null;

            if (root.TryGetProperty("data", out var data) || root.TryGetProperty("Data", out data))
            {
                if (data.ValueKind == JsonValueKind.Array)
                {
                    arrayNode = data;
                }
                else if (data.ValueKind == JsonValueKind.Object)
                {
                    // Caso más común actual: { "status":"ok", "data": { "items": [ {...}, {...} ] } }
                    if (data.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                        arrayNode = itemsProp;
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                arrayNode = root;
            }

            if (arrayNode is { } arr && arr.ValueKind == JsonValueKind.Array)
            {
                var urls = ParseUrlsFromArray(arr);
                if (urls.Count == 0)
                    throw new InvalidOperationException("La API de integración no devolvió imágenes para esta consulta.");
                return urls;
            }

            throw new InvalidOperationException("La API de integración devolvió un formato inesperado de datos.");
        }
        catch
        {
            // Propagar la excepción para que la UI muestre el mensaje concreto.
            throw;
        }
    }

    private static IReadOnlyList<string> ParseUrlsFromArray(JsonElement array)
    {
        var result = new List<string>();

        foreach (var el in array.EnumerateArray())
        {
            string? url = null;
            if (el.ValueKind == JsonValueKind.String)
            {
                url = el.GetString();
            }
            else if (el.ValueKind == JsonValueKind.Object)
            {
                url = TryGetString(el, "url", "Url", "imageUrl", "ImageUrl", "imagen", "Imagen");
            }

            if (string.IsNullOrWhiteSpace(url))
                continue;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(url);
        }

        return result;
    }

    private static string? TryGetString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var s = p.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
        }
        return null;
    }
}

