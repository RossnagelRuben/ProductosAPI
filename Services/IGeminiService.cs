namespace BlazorApp_ProductosAPI.Services;

public interface IGeminiService
{
    Task<GeminiResult> ExtractProductsAsync(byte[] imageBytes, string mimeType, string apiKey);
    /// <summary>Mejora una imagen existente o genera una nueva (text-to-image) con Gemini Nano Banana. prompt: texto a enviar; si imageBytes es null, solo genera desde el prompt.</summary>
    Task<GeminiImageResult?> ImproveOrCreateProductImageAsync(string prompt, string apiKey, byte[]? imageBytes = null, string? mimeType = null);
}

public sealed class GeminiResult
{
    public string ProductosJson { get; set; } = "";
    public string ProveedorJson { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class GeminiImageResult
{
    public bool Success { get; set; }
    public byte[]? ImageBytes { get; set; }
    public string? MimeType { get; set; }
    public string? ErrorMessage { get; set; }
}

