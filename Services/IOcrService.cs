using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public interface IOcrService
{
    Task<OcrResult> ProcessOcrAsync(byte[] imageBytes, string apiKey, double naturalWidth = 800, double naturalHeight = 600);
}

public sealed class OcrResult
{
    public string FullText { get; set; } = "";
    public List<PolygonModel> Polygons { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

