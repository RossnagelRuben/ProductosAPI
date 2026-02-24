using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Servicio para extraer datos de facturas y comprobantes usando la API de Gemini.
/// Usa un HttpClient propio sin cabeceras de autenticación para que la API key vaya solo en la URL (?key=...);
/// el HttpClient inyectado de la app suele llevar Bearer token y Gemini devuelve 401 ACCESS_TOKEN_TYPE_UNSUPPORTED.
/// </summary>
public class GeminiService : IGeminiService
{
    private static readonly HttpClient GeminiHttpClient = new HttpClient();

    /// <summary>
    /// Constructor; el HttpClient inyectado no se usa para Gemini (se usa GeminiHttpClient).
    /// </summary>
    public GeminiService(HttpClient httpClient)
    {
    }

    /// <summary>
    /// Extrae productos y datos de una factura o comprobante usando la API de Gemini.
    /// </summary>
    /// <param name="imageBytes">Bytes de la imagen o PDF a analizar.</param>
    /// <param name="mimeType">Tipo MIME del archivo (image/* o application/pdf).</param>
    /// <param name="apiKey">Clave API de Google para Gemini.</param>
    /// <returns>Resultado con los productos y datos extraídos.</returns>
    public async Task<GeminiResult> ExtractProductsAsync(byte[] imageBytes, string mimeType, string apiKey)
    {
        try
        {
            var prompt = @"Analiza esta factura/comprobante y extrae TODOS los siguientes datos:

1. ENCABEZADO (datos del comprobante):
   - Talonario: número del talonario (ej: ""00014"")
   - Número de comprobante: número que viene despues del numero de talonario (ej: ""00014-000435342"")
   - Tipo de comprobante: identifica el tipo EXACTO según estas categorías:
     * Si es ""X"", ""NO VALIDO COMO COMPROBANTE"", ""NO VALIDO COMO FACTURA"" o ""REMITO"" → usar ""OTROS""
     * Si es ""Factura A"", ""Factura B"" o ""Factura C"" → usar exactamente ""Factura A"", ""Factura B"" o ""Factura C""
     * Si es ""Ticket"" → usar exactamente ""Ticket""
     * Si es ""Nota de Crédito A/B/C"" o ""Nota de Débito A/B/C"" → usar exactamente ""Nota de Crédito A"", ""Nota de Crédito B"", ""Nota de Crédito C"", ""Nota de Débito A"", ""Nota de Débito B"" o ""Nota de Débito C""
   - Fecha del comprobante: fecha que aparece en el documento (FORMATO ISO 8601 COMPLETO con zona horaria: YYYY-MM-DDTHH:mm:ssZ o YYYY-MM-DDTHH:mm:ss+00:00, ej: ""2024-01-15T14:30:00-03:00"")
   - Subtotal: monto previo a impuestos o recargos (usa punto como separador decimal)
   - Total: monto final del comprobante (usa punto como separador decimal)
   - Impuestos/recargos: lista TODOS los impuestos, percepciones o cargos que se suman al subtotal para llegar al total (ej: IVA 21%, Ingresos Brutos/IIBB, tasas municipales, percepciones AFIP, etc.)

2. PROVEEDOR (datos del emisor):
   - CUIT/CUIL: busca el primer CUIT/CUIL que aparezca (generalmente en la parte superior)
   - Razón Social: nombre completo del proveedor/emisor

3. PRODUCTOS: Lista TODOS los ítems de productos/servicios (sin subtotales, impuestos generales ni totales)
   IMPORTANTE: Mantén el orden exacto de los items tal como aparecen en la factura (de arriba hacia abajo).
   Para cada producto extrae:
   - Orden: número de orden del item en la factura (empezando desde 1, de arriba hacia abajo)
   - Código del producto
   - Descripción completa
   - Cantidad
   - Precio unitario
   - IVA: si el producto tiene IVA, indica el porcentaje (normalmente 21% o 10.5%)
   - Descuento: si hay descuento o bonificación aplicado al producto (puede ser porcentaje o monto). NOTA: Descuento y bonificación son lo mismo, usar solo ""descuento""

Devuelve ÚNICAMENTE un JSON válido con esta estructura exacta:
{
  ""encabezado"": {
    ""talonario"": ""00014"",
    ""numero_comprobante"": ""000435342"",
    ""tipo_comprobante"": ""Factura A"",
    ""fecha_comprobante"": ""2024-01-15T14:30:00-03:00"",
    ""subtotal"": ""1000.00"",
    ""total"": ""1210.00"",
    ""impuestos_aplicados"": [
      {
        ""nombre"": ""IVA 21%"",
        ""monto"": ""210.00""
      }
    ]
  },
  ""proveedor"": {
    ""cuit_cuil"": ""20-12345678-9"",
    ""razon_social"": ""Nombre del Proveedor S.A.""
  },
  ""productos"": [
    {
      ""orden"": 1,
      ""codigo"": ""COD001"",
      ""descripcion"": ""Producto 1"",
      ""cantidad"": ""2"",
      ""precio_unitario"": ""100.50"",
      ""iva_porcentaje"": ""21"",
      ""descuento"": ""10%""
    },
    {
      ""orden"": 2,
      ""codigo"": ""COD002"",
      ""descripcion"": ""Producto 2"",
      ""cantidad"": ""1"",
      ""precio_unitario"": ""200.00"",
      ""iva_porcentaje"": ""21"",
      ""descuento"": null
    }
  ]
}

IMPORTANTE: 
- Usa punto (.) como separador decimal
- Devuelve SOLO el JSON, sin texto adicional, sin markdown, sin explicaciones
- Si no encuentras un dato, usa null (no uses ""No encontrado"" ni strings vacíos)
- Para IVA y descuento: si no existen, usa null
- El IVA debe ser solo el número del porcentaje (ej: ""21"" o ""10.5"")
- Descuento puede ser porcentaje (""10%"") o monto fijo (""50.00"")
- El campo ""orden"" es OBLIGATORIO y debe reflejar la posición del item en la factura (1, 2, 3, etc.)
- CLASIFICACIÓN DE TIPO DE COMPROBANTE (CRÍTICO):
  * Si el documento dice ""X"", ""NO VALIDO COMO COMPROBANTE"", ""NO VALIDO COMO FACTURA"" o es un ""REMITO"" → tipo_comprobante debe ser exactamente ""OTROS""
  * Si es una factura válida tipo A, B o C → tipo_comprobante debe ser ""Factura A"", ""Factura B"" o ""Factura C""
  * Si es un ticket → tipo_comprobante debe ser exactamente ""Ticket""
  * Si es una nota de crédito o débito tipo A, B o C → tipo_comprobante debe ser ""Nota de Crédito A/B/C"" o ""Nota de Débito A/B/C""
  * NUNCA clasifiques como factura un documento que diga explícitamente ""NO VALIDO COMO FACTURA"" o ""NO VALIDO COMO COMPROBANTE""";

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

            var res = await GeminiHttpClient.SendAsync(req);
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
                ProductosJson = productosJson, // Contiene el JSON completo con encabezado, proveedor y productos
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

    /// <summary>Mejora una imagen existente (imageBytes != null) o genera una nueva solo con texto (imageBytes == null) usando Gemini Nano Banana.</summary>
    public async Task<GeminiImageResult?> ImproveOrCreateProductImageAsync(string prompt, string apiKey, byte[]? imageBytes = null, string? mimeType = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new GeminiImageResult { Success = false, ErrorMessage = "Falta la API key." };
        if (string.IsNullOrWhiteSpace(prompt))
            return new GeminiImageResult { Success = false, ErrorMessage = "El prompt no puede estar vacío." };
        try
        {
            var model = "gemini-2.5-flash-image";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            object[] parts;
            if (imageBytes != null && imageBytes.Length > 0 && !string.IsNullOrWhiteSpace(mimeType))
            {
                parts = new object[]
                {
                    new { text = prompt },
                    new { inlineData = new { mimeType = mimeType, data = Convert.ToBase64String(imageBytes) } }
                };
            }
            else
            {
                parts = new object[] { new { text = prompt } };
            }
            var body = new
            {
                contents = new[] { new { parts } },
                generationConfig = new { responseModalities = new[] { "TEXT", "IMAGE" } }
            };
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
            var res = await GeminiHttpClient.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return new GeminiImageResult { Success = false, ErrorMessage = $"Gemini {(int)res.StatusCode}: {json}" };
            return ParseGeminiImageResponse(json);
        }
        catch (Exception ex)
        {
            return new GeminiImageResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<GeminiTextResult> GenerateTextAsync(string prompt, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new GeminiTextResult { Success = false, ErrorMessage = "Falta la API key." };
        if (string.IsNullOrWhiteSpace(prompt))
            return new GeminiTextResult { Success = false, ErrorMessage = "El prompt no puede estar vacío." };
        try
        {
            var model = "gemini-2.5-flash-lite";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
            var res = await GeminiHttpClient.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return new GeminiTextResult { Success = false, ErrorMessage = $"Gemini {(int)res.StatusCode}: {json}" };
            var text = ParseGeminiTextResponse(json);
            return new GeminiTextResult { Success = true, Text = text };
        }
        catch (Exception ex)
        {
            return new GeminiTextResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static string? ParseGeminiTextResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
                return null;
            var content = candidates[0].GetProperty("content");
            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0)
                return null;
            return parts[0].GetProperty("text").GetString();
        }
        catch
        {
            return null;
        }
    }

    private static GeminiImageResult? ParseGeminiImageResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
                return new GeminiImageResult { Success = false, ErrorMessage = "Respuesta sin candidates." };
            var content = candidates[0].GetProperty("content");
            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                return new GeminiImageResult { Success = false, ErrorMessage = "Respuesta sin parts." };
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("inlineData", out var inlineData))
                {
                    var mime = inlineData.TryGetProperty("mimeType", out var mt) ? mt.GetString() : "image/png";
                    var data = inlineData.TryGetProperty("data", out var d) ? d.GetString() : null;
                    if (!string.IsNullOrEmpty(data))
                    {
                        var bytes = Convert.FromBase64String(data!);
                        return new GeminiImageResult { Success = true, ImageBytes = bytes, MimeType = mime };
                    }
                }
            }
            return new GeminiImageResult { Success = false, ErrorMessage = "La respuesta no incluyó una imagen." };
        }
        catch (Exception ex)
        {
            return new GeminiImageResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Parsea la respuesta de Gemini y extrae el JSON con los datos extraídos.
    /// </summary>
    /// <param name="responseJson">Respuesta JSON de la API de Gemini.</param>
    /// <returns>Tupla con el JSON completo de productos (incluye encabezado, proveedor y productos) y el JSON del proveedor.</returns>
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

            // Intentar parsear como JSON completo con encabezado, proveedor y productos
            try
            {
                using var responseDoc = JsonDocument.Parse(text);
                var responseRoot = responseDoc.RootElement;

                // Verificar si tiene la estructura completa (encabezado, proveedor, productos)
                if (responseRoot.TryGetProperty("proveedor", out var proveedorElement) &&
                    responseRoot.TryGetProperty("productos", out var productosElement))
                {
                    // Devolver el JSON completo (incluye encabezado si existe)
                    var jsonCompleto = JsonSerializer.Serialize(responseRoot, new JsonSerializerOptions { WriteIndented = true });
                    var proveedorJson = JsonSerializer.Serialize(proveedorElement, new JsonSerializerOptions { WriteIndented = true });
                    return (jsonCompleto, proveedorJson);
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

    /// <summary>
    /// Extrae manualmente los datos del proveedor usando expresiones regulares como fallback.
    /// </summary>
    /// <param name="text">Texto del que extraer los datos.</param>
    /// <returns>JSON con los datos del proveedor extraídos.</returns>
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

