namespace BlazorApp_ProductosAPI.Services.AsignarImagenes;

/// <summary>
/// Abstracción para obtener y guardar imágenes de productos (principio de inversión de dependencias).
/// </summary>
public interface IProductImageService
{
    /// <summary>Obtiene la URL de imagen del producto desde la API (p. ej. Centralizadora).</summary>
    Task<string?> GetImageUrlByCodigoBarraAsync(string? codigoBarra, string token, CancellationToken cancellationToken = default);
    /// <summary>Descarga la imagen desde una URL usando el token Bearer y la devuelve como data URL (para evitar fallos por CORS/auth en &lt;img&gt;).</summary>
    Task<string?> FetchImageAsDataUrlAsync(string imageUrl, string token, CancellationToken cancellationToken = default);
    /// <summary>Guarda la imagen del producto en el backend (API).</summary>
    Task<bool> SaveProductImageAsync(int productoID, string imageUrlOrBase64, string token, CancellationToken cancellationToken = default);
}
