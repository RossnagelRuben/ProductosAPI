namespace BlazorApp_ProductosAPI.Services;

public interface IGeminiService
{
    Task<GeminiResult> ExtractProductsAsync(byte[] imageBytes, string mimeType, string apiKey);
}

public sealed class GeminiResult
{
    public string ProductosJson { get; set; } = "";
    public string ProveedorJson { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

