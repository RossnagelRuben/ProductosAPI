namespace BlazorApp_ProductosAPI.Models.AsignarImagenes;

/// <summary>
/// Filtros de consulta para productos (principio de segregación de interfaces).
/// </summary>
public sealed class ProductoQueryFilter
{
    public int PageSize { get; set; } = 25;
    public int PageNumber { get; set; } = 1;
    public string? CodigoBarra { get; set; }
    public string? DescripcionLarga { get; set; }
    public int FamiliaID { get; set; }
    public int MarcaID { get; set; }
    public short? SucursalID { get; set; }
    public DateTime? FechaModifDesde { get; set; }
    public DateTime? FechaModifHasta { get; set; }
    /// <summary>Filtro por imagen principal: null = todos, true = solo con imagen, false = solo sin imagen.</summary>
    public bool? FiltroImagen { get; set; }
    /// <summary>Filtro por código de barras: null = todos, true = solo con código, false = solo sin código.</summary>
    public bool? FiltroCodigoBarra { get; set; }
}
