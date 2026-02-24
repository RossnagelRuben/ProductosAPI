namespace BlazorApp_ProductosAPI.Services;

public interface IGeminiService
{
    Task<GeminiResult> ExtractProductsAsync(byte[] imageBytes, string mimeType, string apiKey);
    /// <summary>Mejora una imagen existente o genera una nueva (text-to-image) con Gemini Nano Banana. prompt: texto a enviar; si imageBytes es null, solo genera desde el prompt.</summary>
    Task<GeminiImageResult?> ImproveOrCreateProductImageAsync(string prompt, string apiKey, byte[]? imageBytes = null, string? mimeType = null);
    /// <summary>Genera texto desde un prompt (ej. observaciones en RTF a partir de información de búsqueda). Usa la misma API key que el resto de Gemini.</summary>
    Task<GeminiTextResult> GenerateTextAsync(string prompt, string apiKey);
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

/// <summary>Resultado de generación de texto (ej. observaciones en RTF).</summary>
public sealed class GeminiTextResult
{
    public bool Success { get; set; }
    public string? Text { get; set; }
    public string? ErrorMessage { get; set; }
}

