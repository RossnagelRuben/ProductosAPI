using System.Net.Http.Headers;
using System.Text.Json;
using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public class ColectorService : IColectorService
{
    private readonly HttpClient _httpClient;
    private const string ColectorBaseUrl = "https://drrsystemas4.azurewebsites.net/Colector";

    public ColectorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ColectorResult> SendToColectorAsync(string productosJson, int tipoOperacionId, string token)
    {
        try
        {
            var fechaHoraFormateada = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            var productos = ParseProductosToColector(productosJson);

            var payloadJson = $@"{{
  ""colectorEncabID"": 0,
  ""fechaHora"": ""{fechaHoraFormateada}"",
  ""sucursalID"": null,
  ""depositoID"": null,
  ""tipoOperacionID"": {tipoOperacionId},
  ""clienteID"": null,
  ""proveedorID"": null,
  ""registroOperacionID"": null,
  ""estadoID"": null,
  ""pedirAlDepositoID"": null,
  ""colectorItem"": {JsonSerializer.Serialize(productos, new JsonSerializerOptions { WriteIndented = true })}
}}";

            var req = new HttpRequestMessage(HttpMethod.Post, ColectorBaseUrl)
            {
                Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
            };

            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _httpClient.SendAsync(req);
            var responseJson = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return new ColectorResult
                {
                    Success = false,
                    ErrorMessage = $"❌ Error Colector {(int)res.StatusCode} - {res.ReasonPhrase}\n\n" +
                                 $"Respuesta del servidor:\n{responseJson}\n\n" +
                                 $"Payload enviado:\n{payloadJson}",
                    ResponseJson = responseJson
                };
            }

            return new ColectorResult
            {
                Success = true,
                ResponseJson = responseJson
            };
        }
        catch (Exception ex)
        {
            return new ColectorResult
            {
                Success = false,
                ErrorMessage = $"❌ Excepción al enviar al Colector: {ex.Message}"
            };
        }
    }

    public async Task<ColectorResult> ListColectorDataAsync(string token)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, ColectorBaseUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _httpClient.SendAsync(req);
            var responseJson = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return new ColectorResult
                {
                    Success = false,
                    ErrorMessage = $"❌ Error al listar datos del Colector {(int)res.StatusCode} - {res.ReasonPhrase}\n\n" +
                                 $"Respuesta del servidor:\n{responseJson}",
                    ResponseJson = responseJson
                };
            }

            return new ColectorResult
            {
                Success = true,
                ResponseJson = responseJson
            };
        }
        catch (Exception ex)
        {
            return new ColectorResult
            {
                Success = false,
                ErrorMessage = $"❌ Excepción al listar datos del Colector: {ex.Message}"
            };
        }
    }

    private static List<ColectorItem> ParseProductosToColector(string productosJson)
    {
        var items = new List<ColectorItem>();

        if (string.IsNullOrWhiteSpace(productosJson))
        {
            var itemEjemplo = new ColectorItem
            {
                itemColectorID = 0,
                presentacionID = 0,
                codigoID = 11589,
                listaPrecID = 0,
                cantidad = 1.0m,
                codigoBarra = "PRODUCTO_EJEMPLO",
                descripcion = "Producto de ejemplo generado automáticamente",
                cantidadPiezas = 1
            };
            items.Add(itemEjemplo);
            return items;
        }

        var jsonLimpio = productosJson.Trim();
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        List<ProductoItem>? parsed = null;
        try
        {
            parsed = JsonSerializer.Deserialize<List<ProductoItem>>(jsonLimpio, options);
        }
        catch
        {
            try
            {
                var s = jsonLimpio.IndexOf('[');
                var e = jsonLimpio.LastIndexOf(']');

                if (s >= 0 && e > s)
                {
                    var inner = jsonLimpio.Substring(s, e - s + 1);
                    parsed = JsonSerializer.Deserialize<List<ProductoItem>>(inner, options);
                }
            }
            catch { }
        }

        if (parsed == null || parsed.Count == 0)
        {
            var itemEjemplo = new ColectorItem
            {
                itemColectorID = 0,
                presentacionID = 0,
                codigoID = 11589,
                listaPrecID = 0,
                cantidad = 1.0m,
                codigoBarra = "PRODUCTO_EJEMPLO",
                descripcion = "Producto de ejemplo generado automáticamente",
                cantidadPiezas = 1
            };
            items.Add(itemEjemplo);
            return items;
        }

        foreach (var p in parsed)
        {
            decimal cantDec = 0m;
            if (!string.IsNullOrWhiteSpace(p.Cantidad))
            {
                var norm = p.Cantidad.Replace(',', '.');
                decimal.TryParse(norm, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out cantDec);
            }

            int codigoId = 11589;

            var item = new ColectorItem
            {
                itemColectorID = 0,
                presentacionID = 0,
                codigoID = codigoId,
                listaPrecID = 0,
                cantidad = cantDec,
                codigoBarra = p.Descripcion ?? string.Empty,
                descripcion = p.Codigo ?? string.Empty,
                cantidadPiezas = (int)Math.Max(0, Math.Round(cantDec))
            };

            items.Add(item);
        }

        return items;
    }
}

