using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace BlazorApp_ProductosAPI.Services.AsignarImagenes;

/// <summary>
/// Implementación para obtener imágenes desde Centralizadora y guardar en backend (SRP).
/// </summary>
public sealed class ProductImageService : IProductImageService
{
    private const string CentralizadoraUrl = "https://drrsystemas4.azurewebsites.net/Centralizadora/Producto";
    private const string ImagenPlaceholder = "https://ardiaprod.vtexassets.com/arquivos/ids/321558/Yerba-Mate-Amanda-Suave-500-Gr-_1.jpg";
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProductImageService(HttpClient httpClient, IJSRuntime js)
    {
        _httpClient = httpClient;
        _js = js;
    }

    public async Task<string?> GetImageUrlByCodigoBarraAsync(string? codigoBarra, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoBarra))
            return ImagenPlaceholder;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{CentralizadoraUrl}?codigoBarra={Uri.EscapeDataString(codigoBarra.Trim())}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
                return ImagenPlaceholder;
            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return ImagenPlaceholder;
            var data = JsonSerializer.Deserialize<CentralizadoraProducto>(json, JsonOptions);
            var url = !string.IsNullOrWhiteSpace(data?.ImagenWeb) ? data.ImagenWeb : data?.Imagen;
            return string.IsNullOrWhiteSpace(url) ? ImagenPlaceholder : url;
        }
        catch
        {
            return ImagenPlaceholder;
        }
    }

    public async Task<string?> FetchImageAsDataUrlAsync(string imageUrl, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(token)) return null;
        var url = imageUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                await LogAsync("FetchImageAsDataUrl FALLÓ (HTTP)", new { statusCode = (int)resp.StatusCode, status = resp.StatusCode.ToString(), urlPreview = url.Length > 80 ? url.Substring(0, 80) + "…" : url });
                return null;
            }
            var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes == null || bytes.Length == 0)
            {
                await LogAsync("FetchImageAsDataUrl FALLÓ (body vacío)", new { urlPreview = url.Length > 80 ? url.Substring(0, 80) + "…" : url });
                return null;
            }
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                contentType = "image/jpeg";
            var base64 = Convert.ToBase64String(bytes);
            return "data:" + contentType + ";base64," + base64;
        }
        catch (Exception ex)
        {
            await LogAsync("FetchImageAsDataUrl EXCEPCIÓN", new { mensaje = ex.Message, urlPreview = url.Length > 80 ? url.Substring(0, 80) + "…" : url });
            return null;
        }
    }

    private async Task LogAsync(string tag, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            await _js.InvokeVoidAsync("__logAsignarImagenes", tag, json);
        }
        catch { }
    }

    public async Task<bool> SaveProductImageAsync(int productoID, string imageUrlOrBase64, string token, CancellationToken cancellationToken = default)
    {
        // Placeholder: cuando exista API de guardado de imagen, llamarla aquí.
        await Task.CompletedTask;
        return false;
    }

    private sealed class CentralizadoraProducto
    {
        [JsonPropertyName("imagen")] public string? Imagen { get; set; }
        [JsonPropertyName("imagenWeb")] public string? ImagenWeb { get; set; }
    }
}
