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
    private readonly IProductoPatchService _productoPatch;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProductImageService(HttpClient httpClient, IJSRuntime js, IProductoPatchService productoPatch)
    {
        _httpClient = httpClient;
        _js = js;
        _productoPatch = productoPatch;
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
        if (productoID <= 0) return false;
        if (string.IsNullOrWhiteSpace(imageUrlOrBase64)) return false;

        var original = imageUrlOrBase64;

        // Optimizar tamaño de la imagen en el navegador (canvas) antes de enviarla al backend.
        // Solo aplica cuando viene como data URL (data:image/...;base64,AAAA).
        if (imageUrlOrBase64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // Máximo 800x800, calidad alta (0.85) para reducir peso sin perder calidad visible.
                var optimized = await _js.InvokeAsync<string>("__optimizarImagenAsignar", imageUrlOrBase64, 800, 800, 0.85);
                if (!string.IsNullOrWhiteSpace(optimized))
                {
                    imageUrlOrBase64 = optimized;
                }
            }
            catch
            {
                // Si falla la optimización, continuamos con la imagen original.
            }
        }

        // La API espera un string base64 (format: byte). Si viene un data URL ("data:image/...;base64,AAAA"),
        // recortamos el prefijo y enviamos solo la parte base64.
        var imagenParaApi = imageUrlOrBase64;
        if (imageUrlOrBase64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var coma = imageUrlOrBase64.IndexOf(',');
            if (coma >= 0 && coma + 1 < imageUrlOrBase64.Length)
            {
                imagenParaApi = imageUrlOrBase64.Substring(coma + 1);
            }
        }

        var request = new ProductoPatchRequest
        {
            CodigoID = productoID,
            ImagenEspecified = true,
            Imagen = imagenParaApi,
            ObservacionEspecified = false,
            Observacion = "null"
        };

        try
        {
            var result = await _productoPatch.PatchProductoAsync(request, token, cancellationToken);
            if (!result.Success)
            {
                await LogAsync("SaveProductImageAsync PATCH ERROR", new { request, result.StatusCode, result.ResponseBody, result.ErrorMessage });
            }
            else
            {
                await LogAsync("SaveProductImageAsync PATCH OK", new { request, result.StatusCode });
            }
            return result.Success;
        }
        catch (Exception ex)
        {
            await LogAsync("SaveProductImageAsync EXCEPCIÓN", new { productoID, mensaje = ex.Message });
            return false;
        }
    }

    private sealed class CentralizadoraProducto
    {
        [JsonPropertyName("imagen")] public string? Imagen { get; set; }
        [JsonPropertyName("imagenWeb")] public string? ImagenWeb { get; set; }
    }
}
