namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Generación de texto mediante la API de OpenAI (Chat Completions).
/// Responsabilidad única (SOLID): solo expone la generación de texto; la construcción del prompt
/// y el uso del resultado (ej. observaciones RTF) quedan en el consumidor.
/// Referencia: https://developers.openai.com/api/docs
/// </summary>
public interface IOpenAITextService
{
    /// <summary>
    /// Genera texto a partir de un prompt usando el modelo de chat de OpenAI.
    /// </summary>
    /// <param name="prompt">Texto de entrada (instrucciones para el modelo).</param>
    /// <param name="apiKey">API key de OpenAI (Bearer).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado con el texto generado o mensaje de error.</returns>
    Task<OpenAITextResult> GenerateTextAsync(
        string prompt,
        string apiKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de una llamada a generación de texto (alineado con GeminiTextResult para uso homogéneo).
/// </summary>
public sealed class OpenAITextResult
{
    public bool Success { get; set; }
    public string? Text { get; set; }
    public string? ErrorMessage { get; set; }
}
