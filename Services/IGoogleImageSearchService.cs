namespace BlazorApp_ProductosAPI.Services;

/// <summary>Búsqueda de imágenes en Google Custom Search JSON API (código de barras + descripción).</summary>
public interface IGoogleImageSearchService
{
    /// <summary>Busca imágenes con el texto dado. Rota por apiKeys si una falla. Devuelve hasta 10 URLs.</summary>
    Task<IReadOnlyList<string>> SearchImageUrlsAsync(string query, IReadOnlyList<string> apiKeys, string? searchEngineId = null, CancellationToken cancellationToken = default);
    /// <summary>Descarga una imagen por URL y la devuelve como data URL (sin auth, para resultados de búsqueda).</summary>
    Task<string?> FetchImageAsDataUrlAsync(string imageUrl, CancellationToken cancellationToken = default);
}
