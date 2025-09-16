using Microsoft.AspNetCore.Components;

namespace BlazorApp_ProductosAPI.Models
{
    /// <summary>
    /// Modelo para el input de stock en la grilla
    /// </summary>
    public class StockInputModel
    {
        public ProductoStock Producto { get; set; } = new();
        public string Value { get; set; } = string.Empty;
        public Action<ProductoStock, ChangeEventArgs> OnInput { get; set; } = (p, e) => { };
    }
}
