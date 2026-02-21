using BlazorApp_ProductosAPI.Components;
using BlazorApp_ProductosAPI.Components.AsignarImagenes;
using BlazorApp_ProductosAPI.Models;
using BlazorApp_ProductosAPI.Models.AsignarImagenes;
using BlazorApp_ProductosAPI.Services;
using System.Text.Json;
using BlazorApp_ProductosAPI.Services.AsignarImagenes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorApp_ProductosAPI.Pages;

public partial class AsignarImagenes
{
    [Inject] private IProductoQueryService ProductoQuery { get; set; } = null!;
    [Inject] private IProductImageService ProductImage { get; set; } = null!;
    [Inject] private IAuthService Auth { get; set; } = null!;
    [Inject] private ProductoService ProductoService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private IGeminiService GeminiService { get; set; } = null!;
    [Inject] private ILocalStorageService LocalStorage { get; set; } = null!;

    private const string PlaceholderSvg = "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22200%22 height=%22200%22 viewBox=%220 0 200 200%22%3E%3Crect fill=%22%23e9ecef%22 width=%22200%22 height=%22200%22/%3E%3Ctext x=%22100%22 y=%22105%22 text-anchor=%22middle%22 fill=%22%23999%22 font-size=%2214%22%3ESin imagen%3C/text%3E%3C/svg%3E";

    private ProductoQueryFilter _filter = new();
    private string _filtroImagenOption = "Todos";
    private int _busquedaId;
    private List<ProductoConImagenDto> _items = new();
    private List<FamiliaItem> _familias = new();
    private List<MarcaItem> _marcas = new();
    private bool _loading;
    private string? _error;
    private string? _token;
    private int? _loadingImageId;
    private int? _savingImageId;

    private ProductoConImagenDto? _productoModal;
    private string? _imagenModalDataUrl;
    private string _promptModal = "";
    private bool _mejorandoImagen;
    private string? _errorModal;
    private string _geminiApiKeyModal = "";
    private const string LsGeminiKey = "img_gkey";

    private bool _tieneImagenEnModal => !string.IsNullOrWhiteSpace(_imagenModalDataUrl) && !_imagenModalDataUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        await CargarTokenYCatalogos();
    }

    private async Task CargarTokenYCatalogos()
    {
        _token = await Auth.GetTokenFINALAsync();
        if (string.IsNullOrWhiteSpace(_token))
        {
            _error = "Debes iniciar sesión para buscar productos.";
            return;
        }
        _error = null;
        try
        {
            _familias = await ProductoService.GetFamiliasAsync(_token);
            _marcas = await ProductoService.GetMarcasAsync(_token);
            _familias = new List<FamiliaItem> { new FamiliaItem { FamiliaID = 0, Descripcion = "Todas" } }.Concat(_familias).ToList();
            _marcas = new List<MarcaItem> { new MarcaItem { MarcaID = 0, Descripcion = "Todas" } }.Concat(_marcas).ToList();
        }
        catch (Exception ex)
        {
            _error = "Error cargando catálogos: " + ex.Message;
        }
    }

    private async Task Buscar()
    {
        _filter.PageNumber = 1;
        await CargarPaginaAsync();
    }

    /// <summary>Carga la página actual según filtros; con "Con imagen" en página 1 puede pedir varias páginas API hasta completar PageSize.</summary>
    private async Task CargarPaginaAsync()
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            await CargarTokenYCatalogos();
            if (string.IsNullOrWhiteSpace(_token)) return;
        }
        _error = null;
        _loading = true;
        _filter.FiltroImagen = _filtroImagenOption switch
        {
            "Con" => true,
            "Sin" => false,
            _ => null
        };
        try
        {
            _busquedaId++;
            if (_filtroImagenOption == "Con" && _filter.PageNumber == 1)
            {
                // Respetar cantidad: pedir páginas API hasta reunir al menos PageSize productos con imagen
                var acumulada = new List<ProductoConImagenDto>();
                var apiPage = 1;
                const int maxPages = 50;
                while (acumulada.Count < _filter.PageSize && apiPage <= maxPages)
                {
                    _filter.PageNumber = apiPage;
                    var chunk = (await ProductoQuery.GetProductosAsync(_filter, _token)).ToList();
                    if (chunk.Count == 0) break;
                    await CargarImagenesConTokenAsync(chunk);
                    var conImagen = chunk.Where(p => !string.IsNullOrWhiteSpace(p.ImagenUrl)).ToList();
                    acumulada.AddRange(conImagen);
                    if (acumulada.Count >= _filter.PageSize)
                    {
                        _items = acumulada.Take(_filter.PageSize).ToList();
                        break;
                    }
                    if (chunk.Count < _filter.PageSize) { _items = acumulada; break; }
                    apiPage++;
                }
                if (acumulada.Count < _filter.PageSize && acumulada.Count > 0)
                    _items = acumulada;
                _filter.PageNumber = 1; // la pantalla sigue siendo "página 1" para el usuario
            }
            else
            {
                _items = (await ProductoQuery.GetProductosAsync(_filter, _token)).ToList();
                await CargarImagenesConTokenAsync(_items);
                if (_filtroImagenOption == "Con")
                    _items = _items.Where(p => !string.IsNullOrWhiteSpace(p.ImagenUrl)).ToList();
                else if (_filtroImagenOption == "Sin")
                    _items = _items.Where(p => string.IsNullOrWhiteSpace(p.ImagenUrl)).ToList();
            }
            await LogProductosAlConsolaAsync();
        }
        catch (Exception ex)
        {
            _error = "Error buscando productos: " + ex.Message;
            _items = new List<ProductoConImagenDto>();
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private bool PuedeSiguiente => _items.Count == _filter.PageSize;

    private void Primera()
    {
        if (_filter.PageNumber <= 1) return;
        _filter.PageNumber = 1;
        _ = CargarPaginaAsync();
    }

    private void Anterior()
    {
        if (_filter.PageNumber <= 1) return;
        _filter.PageNumber--;
        _ = CargarPaginaAsync();
    }

    private void Siguiente()
    {
        if (!PuedeSiguiente) return;
        _filter.PageNumber++;
        _ = CargarPaginaAsync();
    }

    /// <summary>Descarga cada imagen que sea URL http(s) con el Bearer token y la reemplaza por data URL. Si no se pasa lista, usa _items.</summary>
    private async Task CargarImagenesConTokenAsync(List<ProductoConImagenDto>? lista = null)
    {
        if (string.IsNullOrWhiteSpace(_token)) return;
        var target = lista ?? _items;
        var conUrlHttp = target.Where(p => !string.IsNullOrWhiteSpace(p.ImagenUrl) &&
            (p.ImagenUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || p.ImagenUrl!.StartsWith("https://", StringComparison.OrdinalIgnoreCase))).ToList();
        if (conUrlHttp.Count == 0) return;
        var sem = new SemaphoreSlim(5);
        var resultados = new System.Collections.Concurrent.ConcurrentBag<(int ProductoID, string? Codigo, bool Ok, string? UrlPreview)>();
        var tasks = conUrlHttp.Select(async p =>
        {
            await sem.WaitAsync();
            try
            {
                var urlAntes = p.ImagenUrl;
                var dataUrl = await ProductImage.FetchImageAsDataUrlAsync(p.ImagenUrl!, _token);
                if (dataUrl != null)
                {
                    p.ImagenUrl = dataUrl;
                    resultados.Add((p.ProductoID, p.Codigo, true, null));
                    await InvokeAsync(StateHasChanged);
                }
                else
                    resultados.Add((p.ProductoID, p.Codigo, false, (urlAntes?.Length ?? 0) > 60 ? urlAntes!.Substring(0, 60) + "…" : urlAntes));
            }
            finally
            {
                sem.Release();
            }
        });
        await Task.WhenAll(tasks);
        var detalle = resultados.Select(r => new { r.ProductoID, r.Codigo, ok = r.Ok, urlPreview = r.UrlPreview }).ToList();
        var ok = detalle.Count(r => r.ok);
        await JS.InvokeVoidAsync("__logAsignarImagenes", "CargarImagenesConToken RESUMEN", JsonSerializer.Serialize(new { totalConUrlHttp = conUrlHttp.Count, convertidosOk = ok, fallidos = detalle.Count - ok, detalle }));
    }

    private async Task LogProductosAlConsolaAsync()
    {
        try
        {
            var list = _items.Select(p => new
            {
                p.ProductoID,
                p.Codigo,
                ImagenUrl = p.ImagenUrl ?? "(null)",
                ImagenUrlLongitud = (p.ImagenUrl ?? "").Length,
                p.ImagenCargada
            }).ToList();
            var json = JsonSerializer.Serialize(list);
            await JS.InvokeVoidAsync("__logAsignarImagenes", "Productos recibidos (después de API)", json);
        }
        catch { /* no romper la UI si falla el log */ }
    }

    private void Limpiar()
    {
        _filter = new ProductoQueryFilter();
        _filtroImagenOption = "Todos";
        _filter.PageNumber = 1;
        _items = new List<ProductoConImagenDto>();
        _error = null;
    }

    private async Task AbrirModalImagenAsync(ProductoConImagenDto? p)
    {
        if (p == null) return;
        _productoModal = p;
        var sinImagen = string.IsNullOrWhiteSpace(p.ImagenUrl) || p.ImagenUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase);
        _imagenModalDataUrl = sinImagen ? PlaceholderSvg : p.ImagenUrl;
        _errorModal = null;
        _mejorandoImagen = false;
        _geminiApiKeyModal = await LocalStorage.GetItemAsync(LsGeminiKey) ?? "";
        _promptModal = sinImagen
            ? $"Crea una imagen de producto profesional para catálogo. Fondo blanco o neutro, iluminación de estudio. Producto: {p.DescripcionLarga ?? "—"}. Código: {p.Codigo ?? "—"}. Código de barra: {p.CodigoBarra ?? "—"}. La imagen debe ser realista, solo el producto, estilo fotografía comercial."
            : $"Mejora esta imagen de producto para catálogo. Descripción del producto: {p.DescripcionLarga ?? p.Codigo ?? "—"}. Fondo limpio y profesional, producto bien visible y reconocible.";
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnGeminiKeyInput(ChangeEventArgs e)
    {
        _geminiApiKeyModal = e.Value?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(_geminiApiKeyModal))
            await LocalStorage.SetItemAsync(LsGeminiKey, _geminiApiKeyModal);
    }

    private void CerrarModal()
    {
        _productoModal = null;
        _imagenModalDataUrl = null;
        _promptModal = "";
        _errorModal = null;
        _mejorandoImagen = false;
        StateHasChanged();
    }

    private void OnModalKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            CerrarModal();
    }

    private static (byte[]? bytes, string mime) DataUrlToBytes(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return (null, "image/png");
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) return (null, "image/png");
        var header = dataUrl.Substring(0, comma);
        var mime = "image/png";
        var idx = header.IndexOf("image/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx;
            var end = header.IndexOf(';', start);
            mime = end > 0 ? header.Substring(start, end - start).Trim() : header.Substring(start).Trim();
        }
        try
        {
            var base64 = dataUrl.Substring(comma + 1);
            var bytes = Convert.FromBase64String(base64);
            return (bytes, mime);
        }
        catch { return (null, mime); }
    }

    private async Task MejorarOGenerarImagenAsync()
    {
        if (_productoModal == null)
            return;
        var apiKey = _geminiApiKeyModal?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _errorModal = "Ingresá la API key de Google (Gemini) en el cuadro de arriba.";
            return;
        }
        if (string.IsNullOrWhiteSpace(_promptModal?.Trim()))
        {
            _errorModal = "El prompt no puede estar vacío.";
            return;
        }
        await LocalStorage.SetItemAsync(LsGeminiKey, apiKey);
        _errorModal = null;
        _mejorandoImagen = true;
        StateHasChanged();
        try
        {
            byte[]? bytes = null;
            string? mime = null;
            if (_tieneImagenEnModal)
            {
                var t = DataUrlToBytes(_imagenModalDataUrl);
                bytes = t.bytes;
                mime = t.mime;
                if (bytes == null || bytes.Length == 0)
                {
                    _errorModal = "No se pudo usar la imagen actual.";
                    return;
                }
            }
            var result = await GeminiService.ImproveOrCreateProductImageAsync(_promptModal.Trim(), apiKey, bytes, mime);
            if (result != null && result.Success && result.ImageBytes != null && result.ImageBytes.Length > 0)
            {
                var mimeOut = result.MimeType ?? "image/png";
                _imagenModalDataUrl = "data:" + mimeOut + ";base64," + Convert.ToBase64String(result.ImageBytes);
            }
            else
                _errorModal = result?.ErrorMessage ?? "Gemini no devolvió una imagen.";
        }
        catch (Exception ex)
        {
            _errorModal = ex.Message;
        }
        finally
        {
            _mejorandoImagen = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void GuardarCambiosModal()
    {
        if (_productoModal == null) return;
        var item = _items.FirstOrDefault(x => x.ProductoID == _productoModal.ProductoID);
        if (item != null && !string.IsNullOrWhiteSpace(_imagenModalDataUrl) && !_imagenModalDataUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase))
        {
            item.ImagenUrl = _imagenModalDataUrl;
            item.ImagenCargada = true;
        }
        CerrarModal();
    }

    private async Task GuardarAsync(ProductoConImagenDto? p)
    {
        if (p == null || string.IsNullOrWhiteSpace(_token) || string.IsNullOrWhiteSpace(p.ImagenUrl)) return;
        _savingImageId = p.ProductoID;
        StateHasChanged();
        try
        {
            var ok = await ProductImage.SaveProductImageAsync(p.ProductoID, p.ImagenUrl, _token);
            if (!ok)
                _error = "Guardado de imagen no disponible (API pendiente).";
        }
        finally
        {
            _savingImageId = null;
            await InvokeAsync(StateHasChanged);
        }
    }
}
