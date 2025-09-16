using BlazorApp_ProductosAPI.Models;
using System.Text.Json;

namespace BlazorApp_ProductosAPI.Services;

public class UbicacionService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public UbicacionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<UbicacionResponse?> GetUbicacionesAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(
                "https://drrsystemas4.azurewebsites.net/Deposito/Ubicaciones", 
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<UbicacionResponse>(jsonContent, _jsonOptions);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<UbicacionResponse?> GetUbicacionesAsync(
        string token,
        string? orden,
        int? incluirStock,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(orden))
            {
                query.Add($"Orden={Uri.EscapeDataString(orden)}");
            }
            if (incluirStock.HasValue)
            {
                query.Add($"IncluirStock={incluirStock.Value}"); // 1 = incluir, 2 = solo con stock
            }

            var url = "https://drrsystemas4.azurewebsites.net/Deposito/Ubicaciones";
            if (query.Count > 0)
            {
                url += "?" + string.Join("&", query);
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<UbicacionResponse>(jsonContent, _jsonOptions);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
