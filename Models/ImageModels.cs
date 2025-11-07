using System.Text.Json.Serialization;

namespace BlazorApp_ProductosAPI.Models;

// Modelos para Google Vision API
public sealed class VisionRequest
{
    [JsonPropertyName("requests")]
    public VisionRequestItem[] Requests { get; set; } = Array.Empty<VisionRequestItem>();
}

public sealed class VisionRequestItem
{
    [JsonPropertyName("image")]
    public VisionImage Image { get; set; } = new();

    [JsonPropertyName("features")]
    public VisionFeature[] Features { get; set; } = Array.Empty<VisionFeature>();
}

public sealed class VisionImage
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public sealed class VisionFeature
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "DOCUMENT_TEXT_DETECTION";
}

public sealed class VisionResponse
{
    [JsonPropertyName("responses")]
    public VisionOne[] Responses { get; set; } = Array.Empty<VisionOne>();
}

public sealed class VisionOne
{
    [JsonPropertyName("fullTextAnnotation")]
    public FullTextAnnotation? FullTextAnnotation { get; set; }
}

public sealed class FullTextAnnotation
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("pages")]
    public List<Page> Pages { get; set; } = new();
}

public sealed class Page
{
    [JsonPropertyName("blocks")]
    public List<Block> Blocks { get; set; } = new();
}

public sealed class Block
{
    [JsonPropertyName("boundingBox")]
    public BoundingPoly? BoundingBox { get; set; }

    [JsonPropertyName("paragraphs")]
    public List<Paragraph> Paragraphs { get; set; } = new();
}

public sealed class Paragraph
{
    [JsonPropertyName("words")]
    public List<Word> Words { get; set; } = new();
}

public sealed class Word
{
    [JsonPropertyName("symbols")]
    public List<Symbol> Symbols { get; set; } = new();
}

public sealed class Symbol
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public sealed class BoundingPoly
{
    [JsonPropertyName("vertices")]
    public List<Vertex> Vertices { get; set; } = new();

    [JsonPropertyName("normalizedVertices")]
    public List<NVertex> NormalizedVertices { get; set; } = new();
}

public sealed class Vertex
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    public float Y { get; set; }
}

public sealed class NVertex
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    public float Y { get; set; }
}

// Modelos para procesamiento de imagen
public sealed record Point(float X, float Y);

public sealed class PolygonModel
{
    public List<Point> Points { get; }
    public string PointsAttr => string.Join(' ', Points.Select(p => $"{p.X},{p.Y}"));
    public string Text { get; }

    public PolygonModel(List<Point> pts, string text)
    {
        Points = pts;
        Text = text;
    }
}

public sealed class ImageSize
{
    public double ClientWidth { get; set; }
    public double ClientHeight { get; set; }
    public double NaturalWidth { get; set; }
    public double NaturalHeight { get; set; }
}

// Modelos para Gemini API
public sealed class GeminiRequest
{
    [JsonPropertyName("contents")]
    public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
}

public sealed class GeminiContent
{
    [JsonPropertyName("parts")]
    public object[] Parts { get; set; } = Array.Empty<object>();
}

public sealed class GeminiTextPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public sealed class GeminiImagePart
{
    [JsonPropertyName("inlineData")]
    public GeminiInlineData InlineData { get; set; } = new();
}

public sealed class GeminiInlineData
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";
}

public sealed class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[] Candidates { get; set; } = Array.Empty<GeminiCandidate>();
}

public sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContentResponse Content { get; set; } = new();
}

public sealed class GeminiContentResponse
{
    [JsonPropertyName("parts")]
    public GeminiPartResponse[] Parts { get; set; } = Array.Empty<GeminiPartResponse>();
}

public sealed class GeminiPartResponse
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

// Modelos para productos extraídos
public sealed class ProductoItem
{
    [JsonPropertyName("codigo")]
    public string? Codigo { get; set; }

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    [JsonPropertyName("cantidad")]
    public string? Cantidad { get; set; }

    [JsonPropertyName("precio_unitario")]
    public string? PrecioUnitario { get; set; }
}

public sealed class ProveedorExtraido
{
    [JsonPropertyName("cuit_cuil")]
    public string CuitCuil { get; set; } = "";

    [JsonPropertyName("razon_social")]
    public string RazonSocial { get; set; } = "";
}

public sealed class FacturaExtraida
{
    [JsonPropertyName("proveedor")]
    public ProveedorExtraido? Proveedor { get; set; }

    [JsonPropertyName("productos")]
    public List<ProductoItem> Productos { get; set; } = new();
}

// Modelos para Colector API
public sealed class ColectorPayload
{
    [JsonPropertyName("colectorEncabID")]
    public int ColectorEncabID { get; set; }

    [JsonPropertyName("fechaHora")]
    public string FechaHora { get; set; } = "";

    [JsonPropertyName("sucursalID")]
    public int? SucursalID { get; set; }

    [JsonPropertyName("depositoID")]
    public int? DepositoID { get; set; }

    [JsonPropertyName("tipoOperacionID")]
    public int TipoOperacionID { get; set; }

    [JsonPropertyName("clienteID")]
    public int? ClienteID { get; set; }

    [JsonPropertyName("proveedorID")]
    public int? ProveedorID { get; set; }

    [JsonPropertyName("registroOperacionID")]
    public int? RegistroOperacionID { get; set; }

    [JsonPropertyName("estadoID")]
    public int? EstadoID { get; set; }

    [JsonPropertyName("pedirAlDepositoID")]
    public int? PedirAlDepositoID { get; set; }

    [JsonPropertyName("colectorItem")]
    public List<ColectorItem> ColectorItem { get; set; } = new();
}

public sealed class ColectorItem
{
    public int itemColectorID { get; set; }
    public int presentacionID { get; set; }
    public int codigoID { get; set; }
    public int listaPrecID { get; set; }
    public decimal cantidad { get; set; }
    public string codigoBarra { get; set; } = "";
    public string descripcion { get; set; } = "";
    public int cantidadPiezas { get; set; }
    
    // Propiedades del producto (cuando viene del servidor)
    [JsonPropertyName("producto")]
    public ProductoDetalle? Producto { get; set; }
}

public sealed class ProductoDetalle
{
    [JsonPropertyName("codigoID")]
    public int CodigoID { get; set; }
    
    [JsonPropertyName("rubroCodigo")]
    public string? RubroCodigo { get; set; }
    
    [JsonPropertyName("descripcionLarga")]
    public string? DescripcionLarga { get; set; }
    
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }
}

public sealed class ColectorEncabezado
{
    [JsonPropertyName("colectorEncabID")]
    public int ColectorEncabID { get; set; }
    
    [JsonPropertyName("fechaHora")]
    public string? FechaHora { get; set; }
    
    [JsonPropertyName("colectorItem")]
    public List<ColectorItem>? ColectorItem { get; set; }
}

// Modelos para UI
public sealed class TipoOperacion
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

