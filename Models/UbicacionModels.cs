using System.Text.Json.Serialization;

namespace BlazorApp_ProductosAPI.Models;

public class UbicacionResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("totalRecords")]
    public int TotalRecords { get; set; }

    [JsonPropertyName("data")]
    public List<UbicacionItem> Data { get; set; } = new();
}

public class UbicacionItem
{
    [JsonPropertyName("productoUbicacionID")]
    public int ProductoUbicacionID { get; set; }

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("rutaCompleta")]
    public string? RutaCompleta { get; set; }

    [JsonPropertyName("orden")]
    public string Orden { get; set; } = string.Empty;

    [JsonPropertyName("inhabilitado")]
    public bool? Inhabilitado { get; set; }

    [JsonPropertyName("itemsStock")]
    public List<object> ItemsStock { get; set; } = new();
}

public class ProductoInfo
{
    [JsonPropertyName("codigoID")]
    public int CodigoID { get; set; }

    [JsonPropertyName("rubroCodigo")]
    public string? RubroCodigo { get; set; }

    [JsonPropertyName("descripcionLarga")]
    public string? DescripcionLarga { get; set; }

    [JsonPropertyName("descripcionCorta")]
    public string? DescripcionCorta { get; set; }

    // Soporte para código de barras cuando la API lo provea
    [JsonPropertyName("codigoBarra")]
    public string? CodigoBarra { get; set; }
}

public class ProductoStock
{
    [JsonPropertyName("producto")]
    public ProductoInfo Producto { get; set; } = new();

    [JsonPropertyName("stockActual")]
    public decimal? StockActual { get; set; }

    [JsonPropertyName("stockMinimo")]
    public decimal? StockMinimo { get; set; }

    [JsonPropertyName("stockSugerido")]
    public decimal? StockSugerido { get; set; }

    // Campo local para inventario en UI
    public decimal? CantidadInventario { get; set; }
}

public class FamiliaItem
{
    [JsonPropertyName("familiaID")]
    public int FamiliaID { get; set; }
    
    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
    
    [JsonPropertyName("orden")]
    public string Orden { get; set; } = string.Empty;
}

public class MarcaItem
{
    [JsonPropertyName("marcaID")]
    public int MarcaID { get; set; }
    
    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
}

public class TreeNode
{
    public UbicacionItem Item { get; set; } = new();
    public List<TreeNode> Children { get; set; } = new();
    public TreeNode? Parent { get; set; }
    public bool IsExpanded { get; set; } = false;
    public bool IsVisible { get; set; } = true;
    public bool IsSelected { get; set; } = false;
    public int Level { get; set; } = 0;
    public string Path { get; set; } = string.Empty;
}

public enum NodeType
{
    Empresa,
    Deposito,
    Zona,
    Subzona,
    Otros
}
