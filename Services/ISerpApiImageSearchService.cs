namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Búsqueda de imágenes mediante SerpAPI (Google Images).
/// Documentación: https://serpapi.com/google-images-api y https://serpapi.com/search-api
/// Responsabilidad única: obtener URLs de imágenes para una consulta y ubicación (SOLID: ISP).
/// En errores (API key inválida, sin créditos, fallo de búsqueda) se lanza excepción con el mensaje de la API.
/// </summary>
public interface ISerpApiImageSearchService
{
    /// <summary>
    /// Busca imágenes en Google a través de SerpAPI con la consulta dada.
    /// </summary>
    /// <param name="query">Texto de búsqueda (ej.: código de barras + descripción del producto).</param>
    /// <param name="apiKey">API key de SerpAPI.</param>
    /// <param name="location">Ubicación para la búsqueda (ej.: "Argentina"). Si es null, se usa "Argentina".</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lista de URLs de imágenes (original o thumbnail), hasta un límite razonable.</returns>
    Task<IReadOnlyList<string>> SearchImageUrlsAsync(
        string query,
        string apiKey,
        string? location = null,
        CancellationToken cancellationToken = default);
}
