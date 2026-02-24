using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorApp_ProductosAPI.Services.AsignarImagenes;

/// <summary>
/// Servicio dedicado a enviar cambios parciales de producto (PATCH /Producto) a la API.
/// Sigue SRP: solo conoce el contrato de PATCH y no la UI ni de dónde viene la información.
/// </summary>
public interface IProductoPatchService
{
    /// <summary>
    /// Envía un PATCH de producto a la API usando el token actual.
    /// Solo las propiedades marcadas como "*Specified" se tendrán en cuenta por el backend.
    /// </summary>
    Task<ProductoPatchResult> PatchProductoAsync(ProductoPatchRequest request, string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementación concreta de <see cref="IProductoPatchService"/> contra DRR Api V4.
/// </summary>
public sealed class ProductoPatchService : IProductoPatchService
{
    private const string PatchUrl = "https://drrsystemas4.azurewebsites.net/Producto";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };
    private readonly HttpClient _httpClient;

    public ProductoPatchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProductoPatchResult> PatchProductoAsync(ProductoPatchRequest request, string token, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(token))
            return new ProductoPatchResult { Success = false, ErrorMessage = "Token vacío." };

        using var req = new HttpRequestMessage(HttpMethod.Patch, PatchUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        var json = JsonSerializer.Serialize(request, JsonOptions);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

        using var resp = await _httpClient.SendAsync(req, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        var result = new ProductoPatchResult
        {
            StatusCode = (int)resp.StatusCode,
            ResponseBody = string.IsNullOrWhiteSpace(body) ? null : body
        };

        // Success HTTP + valid JSON con status == "success"
        var apiSuccess = resp.IsSuccessStatusCode;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var st))
                {
                    var s = st.GetString();
                    if (!string.IsNullOrWhiteSpace(s) &&
                        s.Equals("error", StringComparison.OrdinalIgnoreCase))
                    {
                        apiSuccess = false;
                        if (root.TryGetProperty("message", out var msg))
                            result.ErrorMessage = msg.GetString();
                    }
                }
            }
            catch
            {
                // si el cuerpo no es JSON, nos quedamos solo con el estado HTTP
            }
        }

        result.Success = apiSuccess;
        return result;
    }
}

/// <summary>
/// DTO mínimo para PATCH /Producto según Swagger:
/// {
///   "codigoID": 0,
///   "imagenEspecified": true,
///   "imagen": "string",
///   "observacionEspecified": true,
///   "observacion": "string"
/// }
/// </summary>
public sealed class ProductoPatchRequest
{
    [JsonPropertyName("codigoID")]
    public int CodigoID { get; set; }

    [JsonPropertyName("imagenEspecified")]
    public bool ImagenEspecified { get; set; }

    [JsonPropertyName("imagen")]
    public string? Imagen { get; set; }

    [JsonPropertyName("observacionEspecified")]
    public bool ObservacionEspecified { get; set; }

    [JsonPropertyName("observacion")]
    public string? Observacion { get; set; }
}

/// <summary>
/// Resultado detallado de PATCH /Producto para poder inspeccionar estado y cuerpo en logs.
/// </summary>
public sealed class ProductoPatchResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
}


