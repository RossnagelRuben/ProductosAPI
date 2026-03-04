using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Búsqueda de imágenes utilizando la API propia /Integration/ImageSearch.
/// Responsabilidad única: obtener URLs de imágenes para una consulta dada usando el token DRR.
/// </summary>
public interface IIntegrationImageSearchService
{
    /// <summary>
    /// Obtiene URLs de imágenes desde la API /Integration/ImageSearch para la consulta especificada.
    /// </summary>
    /// <param name="query">Texto de búsqueda (por ejemplo, código de barras + descripción).</param>
    /// <param name="token">Token Bearer DRR para autenticar contra la API.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lista de URLs de imágenes (http/https) ya filtradas para mostrar en la UI.</returns>
    Task<IReadOnlyList<string>> SearchImageUrlsAsync(
        string query,
        string token,
        CancellationToken cancellationToken = default);
}

