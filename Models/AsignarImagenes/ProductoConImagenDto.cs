namespace BlazorApp_ProductosAPI.Models.AsignarImagenes;

/// <summary>
/// DTO de producto con datos para asignación de imágenes.
/// Principio de responsabilidad única: solo transporta datos.
/// </summary>
public sealed class ProductoConImagenDto
{
    public int ProductoID { get; set; }
    public string? Codigo { get; set; }
    public string? DescripcionLarga { get; set; }
    public string? CodigoBarra { get; set; }
    /// <summary>Código de rubro del producto (ej. para etiquetado en el grid).</summary>
    public string? RubroCodigo { get; set; }
    public string? Presentacion { get; set; }
    /// <summary>URL de imagen actual (API Centralizadora o guardada).</summary>
    public string? ImagenUrl { get; set; }
    /// <summary>Indica si la imagen fue cargada desde API o está pendiente.</summary>
    public bool ImagenCargada { get; set; }
    /// <summary>Observaciones adicionales del producto (puede venir de la API o editarse en el modal Observaciones). Formato libre o RTF.</summary>
    public string? Observaciones { get; set; }

    /// <summary>
    /// True si la imagen actual fue asignada o modificada en memoria y aún no se ha enviado a la API (PATCH).
    /// Permite acumular cambios y persistirlos todos al hacer clic en "Guardar cambios" (principio de mínima sorpresa).
    /// </summary>
    public bool ImagenPendienteGuardar { get; set; }

    /// <summary>
    /// True si las observaciones fueron generadas o editadas en memoria y aún no se han enviado a la API (PATCH).
    /// </summary>
    public bool ObservacionesPendienteGuardar { get; set; }
}
