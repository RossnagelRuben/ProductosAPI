namespace BlazorApp_ProductosAPI.Models
{
    /// <summary>
    /// Clase para configuración de columnas de la grilla
    /// </summary>
    public class ColumnConfig
    {
        /// <summary>
        /// Clave única de la columna
        /// </summary>
        public string Key { get; set; } = string.Empty;
        
        /// <summary>
        /// Etiqueta visible de la columna
        /// </summary>
        public string Label { get; set; } = string.Empty;
        
        /// <summary>
        /// Indica si la columna está visible
        /// </summary>
        public bool IsVisible { get; set; } = true;
        
        /// <summary>
        /// Ancho de la columna (opcional)
        /// </summary>
        public string? Width { get; set; }
        
        /// <summary>
        /// Indica si la columna es redimensionable
        /// </summary>
        public bool IsResizable { get; set; } = true;
        
        /// <summary>
        /// Orden de la columna
        /// </summary>
        public int Order { get; set; }
    }
}
