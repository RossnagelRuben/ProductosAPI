namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Búsqueda orgánica (web) con SerpAPI (Google Search) para obtener snippets de texto.
/// Se usa para alimentar a Gemini y generar observaciones de productos.
/// </summary>
public interface ISerpApiOrganicSearchService
{
    /// <summary>
    /// Obtiene los snippets (fragmentos de texto) de los resultados orgánicos de Google para la consulta dada.
    /// </summary>
    /// <param name="query">Texto de búsqueda (ej.: descripción del producto + código de barras).</param>
    /// <param name="apiKey">API key de SerpAPI.</param>
    /// <param name="maxSnippets">Máximo de snippets a devolver (por defecto 10).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lista de textos (snippets) de los resultados orgánicos.</returns>
    Task<IReadOnlyList<string>> GetOrganicSnippetsAsync(
        string query,
        string apiKey,
        int maxSnippets = 10,
        CancellationToken cancellationToken = default);
}
