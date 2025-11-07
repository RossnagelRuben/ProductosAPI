using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;

    public GeminiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GeminiResult> ExtractProductsAsync(byte[] imageBytes, string mimeType, string apiKey)
    {
        try
        {
            var prompt = @"Analiza esta factura y extrae:

1. PROVEEDOR: Busca el primer CUIT/CUIL y la primera razón social que aparezcan (generalmente en la parte superior de la factura)

2. PRODUCTOS: Lista todos los ítems de productos/servicios (sin subtotales, impuestos ni totales)

Devuelve ÚNICAMENTE un JSON válido con esta estructura exacta:
{
  ""proveedor"": {
    ""cuit_cuil"": ""20-12345678-9"",
    ""razon_social"": ""Nombre del Proveedor S.A.""
  },
  ""productos"": [
    {
      ""codigo"": ""COD001"",
      ""descripcion"": ""Producto 1"",
      ""cantidad"": ""2"",
      ""precio_unitario"": ""100.50""
    }
  ]
}

IMPORTANTE: 
- Usa punto (.) como separador decimal
- Devuelve SOLO el JSON, sin texto adicional
- Si no encuentras CUIT/CUIL, usa ""No encontrado""
- Si no encuentras razón social, usa ""No encontrado""";

            var model = "gemini-2.5-flash-lite";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var body = new
            {
                contents = new[] {
                    new {
                        parts = new object[] {
                            new { text = "Sos un extractor de ítems de facturas. Devuelve solo JSON válido." },
                            new { text = prompt },
                            new { inlineData = new { mimeType = mimeType, data = Convert.ToBase64String(imageBytes) } }
                        }
                    }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };

            var res = await _httpClient.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return new GeminiResult
                {
                    Success = false,
                    ErrorMessage = $"Gemini error {(int)res.StatusCode} - {res.ReasonPhrase}. Respuesta: {json}"
                };
            }

            var (productosJson, proveedorJson) = ParseGeminiResponse(json);

            return new GeminiResult
            {
                Success = true,
                ProductosJson = productosJson,
                ProveedorJson = proveedorJson
            };
        }
        catch (Exception ex)
        {
            return new GeminiResult
            {
                Success = false,
                ErrorMessage = $"Excepción IA: {ex.Message}"
            };
        }
    }

    private static (string productosJson, string proveedorJson) ParseGeminiResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var candidates = root.GetProperty("candidates");
            if (candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            {
                return (responseJson, "");
            }

            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            if (parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
            {
                return (responseJson, "");
            }

            var text = parts[0].GetProperty("text").GetString() ?? "";

            // Intentar parsear como JSON completo con proveedor y productos
            try
            {
                using var responseDoc = JsonDocument.Parse(text);
                var responseRoot = responseDoc.RootElement;

                if (responseRoot.TryGetProperty("proveedor", out var proveedorElement) &&
                    responseRoot.TryGetProperty("productos", out var productosElement))
                {
                    var proveedorJson = JsonSerializer.Serialize(proveedorElement, new JsonSerializerOptions { WriteIndented = true });
                    var productosJson = JsonSerializer.Serialize(productosElement, new JsonSerializerOptions { WriteIndented = true });
                    return (productosJson, proveedorJson);
                }
                else
                {
                    var proveedorManual = ExtractProveedorManual(text);
                    if (!string.IsNullOrWhiteSpace(proveedorManual))
                    {
                        return (text.Trim(), proveedorManual);
                    }
                }
            }
            catch
            {
                var proveedorManual = ExtractProveedorManual(text);
                if (!string.IsNullOrWhiteSpace(proveedorManual))
                {
                    return (text.Trim(), proveedorManual);
                }
            }

            // Fallback: buscar solo productos
            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');

            if (start >= 0 && end > start)
            {
                return (text.Substring(start, end - start + 1).Trim(), "");
            }

            return (text.Trim(), "");
        }
        catch
        {
            return (responseJson, "");
        }
    }

    private static string ExtractProveedorManual(string text)
    {
        try
        {
            var cuitPattern = @"\b\d{2}-\d{8}-\d{1}\b";
            var cuilPattern = @"\b\d{2}-\d{7}-\d{1}\b";
            var cuitMatch = Regex.Match(text, cuitPattern);
            var cuilMatch = Regex.Match(text, cuilPattern);

            var cuitCuil = cuitMatch.Success ? cuitMatch.Value : (cuilMatch.Success ? cuilMatch.Value : "No encontrado");

            var empresaPattern = @"[A-ZÁÉÍÓÚÑ][A-Za-záéíóúñ\s\.&,]+(?:S\.A\.|S\.R\.L\.|S\.A\.S\.|S\.H\.|S\.C\.A\.|LTD\.|INC\.|CORP\.)";
            var empresaMatch = Regex.Match(text, empresaPattern);
            var razonSocial = empresaMatch.Success ? empresaMatch.Value.Trim() : "No encontrado";

            if (razonSocial == "No encontrado")
            {
                var lineas = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var linea in lineas)
                {
                    var lineaTrim = linea.Trim();
                    if (lineaTrim.Length > 5 && lineaTrim.Length < 100 &&
                        char.IsUpper(lineaTrim[0]) &&
                        !lineaTrim.Contains('[') && !lineaTrim.Contains(']') &&
                        !lineaTrim.Contains('{') && !lineaTrim.Contains('}'))
                    {
                        razonSocial = lineaTrim;
                        break;
                    }
                }
            }

            var proveedor = new
            {
                cuit_cuil = cuitCuil,
                razon_social = razonSocial
            };

            return JsonSerializer.Serialize(proveedor, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return "";
        }
    }
}

