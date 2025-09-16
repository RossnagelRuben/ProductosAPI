using System.Text.Json;
using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public class ProductoService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProductoService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<FamiliaItem>> GetFamiliasAsync(string token, CancellationToken cancellationToken = default)
    {
        var result = new List<FamiliaItem>();
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync("https://drrsystemas4.azurewebsites.net/Producto/Familia", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return result;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            // La API suele devolver { data: [...] }
            JsonElement root = doc.RootElement;
            JsonElement listElem;
            if (root.TryGetProperty("data", out listElem) && listElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in listElem.EnumerateArray())
                {
                    var item = ParseFamilia(el);
                    if (item != null) result.Add(item);
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray())
                {
                    var item = ParseFamilia(el);
                    if (item != null) result.Add(item);
                }
            }
        }
        catch
        {
            // ignore
        }
        return result;
    }

    private static FamiliaItem? ParseFamilia(JsonElement el)
    {
        try
        {
            int id = 0;
            string desc = string.Empty;
            if (el.TryGetProperty("familiaID", out var idProp) && idProp.TryGetInt32(out var v))
                id = v;
            else if (el.TryGetProperty("FamiliaID", out var idProp2) && idProp2.TryGetInt32(out var v2))
                id = v2;

            if (el.TryGetProperty("descripcion", out var dProp))
                desc = dProp.GetString() ?? string.Empty;
            else if (el.TryGetProperty("Descripcion", out var dProp2))
                desc = dProp2.GetString() ?? string.Empty;

            if (id == 0 && string.IsNullOrWhiteSpace(desc)) return null;
            return new FamiliaItem { FamiliaID = id, Descripcion = desc };
        }
        catch
        {
            return null;
        }
    }
}


