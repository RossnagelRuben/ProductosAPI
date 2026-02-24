namespace BlazorApp_ProductosAPI.Models.AsignarImagenes;

/// <summary>
/// Estado del modal "Buscar imagen en la web" (Single Responsibility).
/// Agrupa ProductoID, URLs y flags en un solo objeto: al abrir para otro producto se crea
/// un estado nuevo y el clic en imagen recibe el ProductoID desde la vista, evitando
/// asignar la imagen al producto equivocado (2º, 3er producto).
/// </summary>
public sealed class BusquedaWebState
{
    /// <summary>Producto para el que se abrió la búsqueda; no cambiar hasta cerrar o seleccionar.</summary>
    public int ProductoID { get; set; }
    public string? DescripcionLarga { get; set; }
    public string? CodigoBarra { get; set; }
    public List<string>? Urls { get; set; }
    public bool Loading { get; set; }
    public bool Descargando { get; set; }
    public string? Error { get; set; }

    public bool TieneResultados => Urls != null && Urls.Count > 0;

    /// <summary>Etiqueta del origen de la búsqueda para el mensaje del modal (ej.: "Google", "SerpAPI").</summary>
    public string? SourceLabel { get; set; }

    public static BusquedaWebState Vacío => new() { ProductoID = 0 };

    public bool EsParaProducto(int productId) => ProductoID != 0 && ProductoID == productId;
}
