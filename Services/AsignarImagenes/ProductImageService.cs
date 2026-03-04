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

        var formatoAntes = imageUrlOrBase64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            ? (imageUrlOrBase64.IndexOf(';') is int idx && idx > 10 ? imageUrlOrBase64.Substring(5, idx - 5) : "data:image/?")
            : "no-data-url";
        var lenAntes = imageUrlOrBase64.Length;

        // Optimizar y convertir a JPEG en el navegador (canvas) antes de enviar al backend.
        if (imageUrlOrBase64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var optimized = await _js.InvokeAsync<string>("__optimizarImagenAsignar", imageUrlOrBase64, 800, 800, 0.85);
                if (!string.IsNullOrWhiteSpace(optimized))
                    imageUrlOrBase64 = optimized;
            }
            catch (Exception exOpt)
            {
                await LogAsync("SaveProductImageAsync OPTIMIZADOR EXCEPCIÓN", new { productoID, mensaje = exOpt.Message });
            }
            // Si tras optimizar no es JPEG (falló canvas), forzar conversión solo a JPEG.
            if (!imageUrlOrBase64.StartsWith("data:image/jpeg", StringComparison.OrdinalIgnoreCase) && !imageUrlOrBase64.StartsWith("data:image/jpg", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var jpegOnly = await _js.InvokeAsync<string>("__convertirImagenAJpeg", imageUrlOrBase64, 0.92);
                    if (!string.IsNullOrWhiteSpace(jpegOnly) && jpegOnly.StartsWith("data:image/jpeg", StringComparison.OrdinalIgnoreCase))
                        imageUrlOrBase64 = jpegOnly;
                }
                catch { }
            }
        }

        var formatoDespues = imageUrlOrBase64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            ? (imageUrlOrBase64.IndexOf(';') is int i && i > 10 ? imageUrlOrBase64.Substring(5, i - 5) : "data:image/?")
            : "no-data-url";

        // Extraer solo base64 y quitar espacios/saltos que rompen el decoding en el servidor.
        var imagenParaApi = imageUrlOrBase64;
        if (imageUrlOrBase64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var coma = imageUrlOrBase64.IndexOf(',');
            if (coma >= 0 && coma + 1 < imageUrlOrBase64.Length)
            {
                imagenParaApi = imageUrlOrBase64.Substring(coma + 1);
            }
        }
        imagenParaApi = SanitizeBase64(imagenParaApi);

        var base64Len = imagenParaApi?.Length ?? 0;
        var base64Preview = base64Len > 0 && imagenParaApi != null
            ? (imagenParaApi.Length <= 60 ? imagenParaApi : imagenParaApi.Substring(0, 60) + "...")
            : "(vacío)";

        var esJpeg = formatoDespues.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || formatoDespues.Contains("jpg", StringComparison.OrdinalIgnoreCase);
        await LogAsync("SaveProductImageAsync ANTES PATCH", new
        {
            productoID,
            formatoAntes,
            formatoDespues,
            lenAntes,
            base64Len,
            base64Preview,
            esJpeg
        });

        // La API DRR solo acepta JPEG. Si la conversión en el navegador falló (ej. WebP), no enviar y evitar "Parameter is not valid".
        if (!esJpeg)
        {
            await LogAsync("SaveProductImageAsync OMITIDO (no JPEG)", new
            {
                productoID,
                formatoDespues,
                mensaje = "La imagen no pudo convertirse a JPEG (ej. WebP). La API solo acepta JPEG. No se envía PATCH."
            });
            return false;
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
                await LogAsync("SaveProductImageAsync PATCH ERROR", new
                {
                    productoID,
                    StatusCode = result.StatusCode,
                    ResponseBody = result.ResponseBody,
                    ErrorMessage = result.ErrorMessage,
                    base64Len,
                    formatoEnviado = formatoDespues
                });
            }
            else
            {
                await LogAsync("SaveProductImageAsync PATCH OK", new { productoID, StatusCode = result.StatusCode, base64Len });
            }
            return result.Success;
        }
        catch (Exception ex)
        {
            await LogAsync("SaveProductImageAsync EXCEPCIÓN", new { productoID, mensaje = ex.Message });
            return false;
        }
    }

    /// <summary>Quita espacios, saltos de línea y retornos para que el servidor decodifique el base64 correctamente.</summary>
    private static string SanitizeBase64(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return base64 ?? "";
        var sb = new System.Text.StringBuilder(base64.Length);
        foreach (var c in base64)
        {
            if (char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=')
                sb.Append(c);
        }
        return sb.ToString();
    }

    private sealed class CentralizadoraProducto
    {
        [JsonPropertyName("imagen")] public string? Imagen { get; set; }
        [JsonPropertyName("imagenWeb")] public string? ImagenWeb { get; set; }
    }
}
