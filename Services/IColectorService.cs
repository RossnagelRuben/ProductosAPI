using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public interface IColectorService
{
    Task<ColectorResult> SendToColectorAsync(string productosJson, int tipoOperacionId, string token);
    Task<ColectorResult> ListColectorDataAsync(string token);
}

public sealed class ColectorResult
{
    public bool Success { get; set; }
    public string? ResponseJson { get; set; }
    public string? ErrorMessage { get; set; }
}

