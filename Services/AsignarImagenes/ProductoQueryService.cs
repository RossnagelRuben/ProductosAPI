using System.Net.Http.Headers;
using System.Text.Json;
using BlazorApp_ProductosAPI.Models.AsignarImagenes;
using Microsoft.JSInterop;

namespace BlazorApp_ProductosAPI.Services.AsignarImagenes;

/// <summary>
/// Implementación de consulta de productos contra la API (principio de responsabilidad única).
/// </summary>
public sealed class ProductoQueryService : IProductoQueryService
{
    private const string ApiUrl = "https://drrsystemas4.azurewebsites.net/Producto/GetProducto";
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProductoQueryService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<IReadOnlyList<ProductoConImagenDto>> GetProductosAsync(ProductoQueryFilter filter, string token, CancellationToken cancellationToken = default)
    {
        var qs = new List<string>
        {
            $"pageSize={Math.Clamp(filter.PageSize, 1, 500)}",
            $"pageNumber={Math.Max(1, filter.PageNumber)}"
        };
        if (!string.IsNullOrWhiteSpace(filter.CodigoBarra))
            qs.Add($"codigoBarra={Uri.EscapeDataString(filter.CodigoBarra.Trim())}");
        if (!string.IsNullOrWhiteSpace(filter.DescripcionLarga))
            qs.Add($"descripcionLarga={Uri.EscapeDataString(filter.DescripcionLarga.Trim())}");
        if (filter.FamiliaID != 0)
            qs.Add($"familiaID={filter.FamiliaID}");
        if (filter.MarcaID != 0)
            qs.Add($"marcaID={filter.MarcaID}");
        if (filter.SucursalID.HasValue)
            qs.Add($"sucursalID={filter.SucursalID.Value}");
        if (filter.FechaModifDesde.HasValue)
            qs.Add($"fechaModifDesde={filter.FechaModifDesde.Value:yyyy-MM-dd}");
        if (filter.FechaModifHasta.HasValue)
            qs.Add($"fechaModifHasta={filter.FechaModifHasta.Value:yyyy-MM-dd}");
        if (filter.FiltroImagen.HasValue)
            qs.Add($"Imagen={filter.FiltroImagen.Value.ToString().ToLowerInvariant()}");
        // Si la API GetProducto acepta filtro por código de barras, lo enviamos para que el servidor filtre.
        if (filter.FiltroCodigoBarra.HasValue)
            qs.Add($"ConCodigoBarra={filter.FiltroCodigoBarra.Value.ToString().ToLowerInvariant()}");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}?{string.Join("&", qs)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _httpClient.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
            return Array.Empty<ProductoConImagenDto>();

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        var list = new List<ProductoConImagenDto>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var dataArr) && !root.TryGetProperty("Data", out dataArr))
                return list;
            if (dataArr.ValueKind != JsonValueKind.Array)
                return list;
            var index = 0;
            foreach (var el in dataArr.EnumerateArray())
            {
                if (index == 0)
                    await LogEstructuraImagenPrimerProductoAsync(el);
                index++;
                var dto = MapToDto(el);
                if (dto != null)
                    list.Add(dto);
            }
        }
        catch
        {
            // devolver lista vacía en caso de error de parsing
        }
        return list;
    }

    private static ProductoConImagenDto? MapToDto(JsonElement el)
    {
        try
        {
            var codigoID = 0;
            if (el.TryGetProperty("codigoID", out var cid) && cid.TryGetInt32(out var v))
                codigoID = v;
            var codigo = el.TryGetProperty("codigoFabrica", out var cf) ? cf.GetString() : null;
            if (string.IsNullOrWhiteSpace(codigo))
                codigo = codigoID.ToString();
            var descripcionLarga = el.TryGetProperty("descripcionLarga", out var dl) ? dl.GetString() : null;
            string? codigoBarra = GetCodigoBarraFromElement(el);
            string? presentacion = "Unidad";
            if (el.TryGetProperty("presentaciones", out var pres) && pres.ValueKind == JsonValueKind.Array && pres.GetArrayLength() > 0)
            {
                var first = pres[0];
                if (first.TryGetProperty("presentacionID", out var pid))
                    presentacion = pid.ValueKind == JsonValueKind.Number && pid.GetInt32() == 0 ? "Unidad" : pid.GetString() ?? "Unidad";
            }
            string? imagenUrl = GetImageUrlFromElement(el);
            imagenUrl = NormalizeImageUrl(imagenUrl);
            return new ProductoConImagenDto
            {
                ProductoID = codigoID,
                Codigo = codigo,
                DescripcionLarga = descripcionLarga,
                CodigoBarra = codigoBarra,
                Presentacion = presentacion,
                ImagenUrl = imagenUrl,
                ImagenCargada = !string.IsNullOrWhiteSpace(imagenUrl)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Obtiene el código de barras probando varias rutas y nombres (camelCase y PascalCase) del JSON de la API.</summary>
    private static string? GetCodigoBarraFromElement(JsonElement el)
    {
        var barraNames = new[] { "codigoBarra", "CodigoBarra", "codigo_de_barras", "codigoDeBarras", "ean", "Ean", "gtin", "Gtin", "barcode", "Barcode" };
        foreach (var name in barraNames)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        if (el.TryGetProperty("presentaciones", out var pres) && pres.ValueKind == JsonValueKind.Array)
        {
            foreach (var pre in pres.EnumerateArray())
            {
                foreach (var name in barraNames)
                {
                    if (pre.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
                var listNames = new[] { "listaCodigoBarra", "ListaCodigoBarra", "listaCodigoBarras", "codigosBarra" };
                foreach (var listName in listNames)
                {
                    if (!pre.TryGetProperty(listName, out var lcb) || lcb.ValueKind != JsonValueKind.Array) continue;
                    foreach (var cb in lcb.EnumerateArray())
                    {
                        foreach (var name in barraNames)
                        {
                            if (cb.TryGetProperty(name, out var cbVal) && cbVal.ValueKind == JsonValueKind.String)
                            {
                                var s = cbVal.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) return s;
                            }
                        }
                    }
                }
            }
        }
        if (el.TryGetProperty("Presentaciones", out var presP) && presP.ValueKind == JsonValueKind.Array)
        {
            foreach (var pre in presP.EnumerateArray())
            {
                foreach (var name in barraNames)
                {
                    if (pre.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
                var listNames = new[] { "listaCodigoBarra", "ListaCodigoBarra" };
                foreach (var listName in listNames)
                {
                    if (!pre.TryGetProperty(listName, out var lcb) || lcb.ValueKind != JsonValueKind.Array) continue;
                    foreach (var cb in lcb.EnumerateArray())
                    {
                        foreach (var name in barraNames)
                        {
                            if (cb.TryGetProperty(name, out var cbVal) && cbVal.ValueKind == JsonValueKind.String)
                            {
                                var s = cbVal.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) return s;
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    /// <summary>Obtiene la URL de imagen del producto: strings directos, objetos con url, o primer elemento de arrays (imagenes/Imagenes).</summary>
    private static string? GetImageUrlFromElement(JsonElement el)
    {
        var directNames = new[] { "imagenWeb", "ImagenWeb", "imagen", "Imagen", "imagenPrincipal", "ImagenPrincipal", "imagenUrl", "ImagenUrl" };
        foreach (var name in directNames)
        {
            if (!el.TryGetProperty(name, out var prop))
                continue;
            var url = GetUrlFromProp(prop);
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }
        var arrayNames = new[] { "imagenes", "Imagenes", "listaImagenes", "ListaImagenes" };
        foreach (var name in arrayNames)
        {
            if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
                continue;
            var first = arr[0];
            var url = GetUrlFromProp(first);
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }
        foreach (var prop in el.EnumerateObject())
        {
            if (!prop.Name.Contains("imagen", StringComparison.OrdinalIgnoreCase))
                continue;
            var url = GetUrlFromProp(prop.Value);
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }
        return null;
    }

    private static string? GetUrlFromProp(JsonElement prop)
    {
        if (prop.ValueKind == JsonValueKind.String)
        {
            var s = prop.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }
        if (prop.ValueKind == JsonValueKind.Object)
            return TryGetString(prop, "url", "Url", "ruta", "Ruta", "imagen", "Imagen", "imagenWeb", "ImagenWeb");
        return null;
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

    private const string ApiBaseUrl = "https://drrsystemas4.azurewebsites.net";

    /// <summary>Convierte a data URL cuando la API devuelve base64 puro o base64 como path de URL (ej. /9j/4AAQ... o https://servidor/9j/4AAQ...).</summary>
    private static string? NormalizeImageUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        raw = raw.Trim();
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return raw;
        if (raw.StartsWith("//"))
            raw = "https:" + raw;

        // Path solo (para detectar base64 en URL). NO quitar la barra inicial: en JPEG el base64 empieza por "/9j/4AAQ" y esa "/" es parte del payload.
        string pathOnly = raw;
        if (raw.StartsWith(ApiBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            var sinQuery = raw.Contains('?') ? raw.Substring(0, raw.IndexOf('?')) : raw;
            // Sin TrimStart('/'): el base64 de JPEG empieza por "/9j/4AAQ", esa barra es parte del payload
            pathOnly = sinQuery.Length > ApiBaseUrl.Length ? sinQuery.Substring(ApiBaseUrl.Length) : "";
        }
        else if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                pathOnly = uri.AbsolutePath;
            else
            {
                var sinQuery = raw.Contains('?') ? raw.Substring(0, raw.IndexOf('?')) : raw;
                var slashSlash = sinQuery.IndexOf("//", StringComparison.OrdinalIgnoreCase);
                var afterAuthority = slashSlash >= 0 ? sinQuery.IndexOf('/', slashSlash + 2) : -1;
                pathOnly = afterAuthority >= 0 ? sinQuery.Substring(afterAuthority) : "";
            }
        }
        else if (raw.StartsWith("/"))
            pathOnly = raw;

        // Base64 puede venir como path: normalizar URL-safe (- → +, _ → /) para poder decodificar
        string pathNormalized = pathOnly.Replace('-', '+').Replace('_', '/');

        // Si el valor es base64 puro (sin http) o el path de la URL es base64 → data URL
        if (LooksLikeBase64(pathNormalized) && pathNormalized.Length > 20)
        {
            var mime = GetMimeFromBase64Content(pathNormalized);
            return "data:" + mime + ";base64," + pathNormalized;
        }

        // Si era path relativo y no era base64, devolver URL absoluta
        if (raw.StartsWith("/"))
            return ApiBaseUrl.TrimEnd('/') + raw;
        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return raw;
        // Base64 puro sin prefijo (ya lo intentamos con pathNormalized; aquí raw es el string crudo)
        if (LooksLikeBase64(raw))
        {
            var mime = GetMimeFromBase64Content(raw);
            return "data:" + mime + ";base64," + raw;
        }
        return ApiBaseUrl.TrimEnd('/') + "/" + raw.TrimStart('/');
    }

    /// <summary>Detecta el MIME type (image/jpeg, image/png, image/gif, image/webp) a partir del contenido base64 para que el navegador muestre la imagen correctamente.</summary>
    private static string GetMimeFromBase64Content(string base64)
    {
        try
        {
            var take = Math.Min(base64.Length, 28);
            if (take < 4) return "image/jpeg";
            take = (take / 4) * 4;
            var chunk = base64.Substring(0, take);
            var bytes = Convert.FromBase64String(chunk);
            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "image/png";
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "image/jpeg";
            if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
                return "image/gif";
            if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                return "image/webp";
        }
        catch { }
        return "image/jpeg";
    }

    private static bool LooksLikeBase64(string s)
    {
        if (s.Length < 20) return false;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=') continue;
            if (char.IsWhiteSpace(c)) continue;
            return false;
        }
        return true;
    }

    /// <summary>Log en consola F12: estructura del JSON del primer producto (campos de imagen) para diagnosticar formato.</summary>
    private async Task LogEstructuraImagenPrimerProductoAsync(JsonElement el)
    {
        try
        {
            var todasLasPropiedades = new List<string>();
            var camposImagen = new Dictionary<string, object>();
            foreach (var prop in el.EnumerateObject())
            {
                var name = prop.Name;
                todasLasPropiedades.Add(name);
                if (!name.Contains("imagen", StringComparison.OrdinalIgnoreCase))
                    continue;
                var val = prop.Value;
                object desc;
                switch (val.ValueKind)
                {
                    case JsonValueKind.String:
                        var s = val.GetString() ?? "";
                        desc = new
                        {
                            tipo = "String",
                            longitud = s.Length,
                            preview = s.Length <= 200 ? s : s.Substring(0, 200) + "...",
                            empiezaConData = s.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase),
                            empiezaConHttp = s.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        };
                        break;
                    case JsonValueKind.Object:
                        var inner = new List<string>();
                        foreach (var p in val.EnumerateObject())
                            inner.Add(p.Name + ": " + p.Value.ValueKind);
                        desc = new { tipo = "Object", propiedades = inner };
                        break;
                    case JsonValueKind.Array:
                        var len = val.GetArrayLength();
                        desc = new { tipo = "Array", longitud = len, primerElementoKind = len > 0 ? val[0].ValueKind.ToString() : (object)"(vacío)" };
                        break;
                    case JsonValueKind.Null:
                        desc = new { tipo = "Null" };
                        break;
                    default:
                        desc = new { tipo = val.ValueKind.ToString() };
                        break;
                }
                camposImagen[name] = desc;
            }
            var resumenObj = new Dictionary<string, object> { ["resumen"] = camposImagen.Count == 0 ? "Ningún campo con 'imagen' en el nombre." : string.Join(" | ", camposImagen.Keys) };
            if (camposImagen.Count > 0)
                resumenObj["camposImagen"] = camposImagen;
            await _jsRuntime.InvokeVoidAsync("__logAsignarImagenes", "Resumen imagen (1 línea)", JsonSerializer.Serialize(resumenObj));

            var propsBarra = todasLasPropiedades.Where(x => x.IndexOf("barra", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf("codigo", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf("presentacion", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var estructuraPresentaciones = new List<object>();
            if (el.TryGetProperty("presentaciones", out var presLog) && presLog.ValueKind == JsonValueKind.Array && presLog.GetArrayLength() > 0)
            {
                var firstPre = presLog[0];
                estructuraPresentaciones.Add(new { tipo = "presentaciones[0]", propiedades = firstPre.EnumerateObject().Select(p => p.Name + ": " + p.Value.ValueKind).ToList() });
                if (firstPre.TryGetProperty("listaCodigoBarra", out var lcbLog) && lcbLog.ValueKind == JsonValueKind.Array && lcbLog.GetArrayLength() > 0)
                    estructuraPresentaciones.Add(new { tipo = "listaCodigoBarra[0]", propiedades = lcbLog[0].EnumerateObject().Select(p => p.Name + ": " + p.Value.ValueKind).ToList() });
                if (firstPre.TryGetProperty("ListaCodigoBarra", out var lcbP) && lcbP.ValueKind == JsonValueKind.Array && lcbP.GetArrayLength() > 0)
                    estructuraPresentaciones.Add(new { tipo = "ListaCodigoBarra[0]", propiedades = lcbP[0].EnumerateObject().Select(p => p.Name + ": " + p.Value.ValueKind).ToList() });
            }
            var payload = new
            {
                mensaje = "Estructura del primer producto (API GetProducto) - revisar formato de imagen",
                todasLasPropiedades = todasLasPropiedades.OrderBy(x => x).ToList(),
                camposRelacionadosConImagen = camposImagen,
                propiedadesCodigoBarraPresentaciones = propsBarra,
                estructuraPresentaciones = estructuraPresentaciones
            };
            var json = JsonSerializer.Serialize(payload);
            await _jsRuntime.InvokeVoidAsync("__logAsignarImagenes", "API JSON - formato imagen (primer producto)", json);
        }
        catch
        {
            // no romper el flujo si falla el log
        }
    }
}
