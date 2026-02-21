using BlazorApp_ProductosAPI.Models.AsignarImagenes;

namespace BlazorApp_ProductosAPI.Services.AsignarImagenes;

/// <summary>
/// Abstracción para consultar productos (principio de inversión de dependencias).
/// </summary>
public interface IProductoQueryService
{
    Task<IReadOnlyList<ProductoConImagenDto>> GetProductosAsync(ProductoQueryFilter filter, string token, CancellationToken cancellationToken = default);
}
