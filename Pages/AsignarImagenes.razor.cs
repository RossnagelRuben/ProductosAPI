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
    [Inject] private IGoogleImageSearchService GoogleImageSearch { get; set; } = null!;
    [Inject] private ISerpApiImageSearchService SerpApiImageSearch { get; set; } = null!;

    private const string PlaceholderSvg = "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22200%22 height=%22200%22 viewBox=%220 0 200 200%22%3E%3Crect fill=%22%23e9ecef%22 width=%22200%22 height=%22200%22/%3E%3Ctext x=%22100%22 y=%22105%22 text-anchor=%22middle%22 fill=%22%23999%22 font-size=%2214%22%3ESin imagen%3C/text%3E%3C/svg%3E";

    private ProductoQueryFilter _filter = new();
    private string _filtroImagenOption = "Todos";
    private string _filtroCodigoBarraOption = "Todos";
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
    private const string LsGoogleSearchKeys = "google_search_keys";
    private const string LsSerpApiKey = "serpapi_key";

    private string _googleSearchKeysRaw = "";
    /// <summary>API key de SerpAPI para búsqueda de imágenes (Google Images, ubicación Argentina).</summary>
    private string _serpApiKeyRaw = "";
    /// <summary>Estado del modal "Buscar imagen en la web". Null = cerrado. Evita referencias obsoletas al 2º/3er producto (SOLID: estado único).</summary>
    private BusquedaWebState? _busquedaWeb;
    /// <summary>Cancelación de la descarga actual para no quedar en "Descargando imagen" y poder elegir otra.</summary>
    private CancellationTokenSource? _busquedaWebCts;

    private bool _tieneImagenEnModal => !string.IsNullOrWhiteSpace(_imagenModalDataUrl) && !_imagenModalDataUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        await CargarTokenYCatalogos();
        _googleSearchKeysRaw = await LocalStorage.GetItemAsync(LsGoogleSearchKeys) ?? "";
        _serpApiKeyRaw = await LocalStorage.GetItemAsync(LsSerpApiKey) ?? "";
    }

    private async Task OnGoogleSearchKeysInput(ChangeEventArgs e)
    {
        _googleSearchKeysRaw = e.Value?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(_googleSearchKeysRaw))
            await LocalStorage.SetItemAsync(LsGoogleSearchKeys, _googleSearchKeysRaw);
    }

    private async Task OnSerpApiKeyInput(ChangeEventArgs e)
    {
        _serpApiKeyRaw = e.Value?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(_serpApiKeyRaw))
            await LocalStorage.SetItemAsync(LsSerpApiKey, _serpApiKeyRaw);
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

    /// <summary>Carga la página actual según filtros. Cuando hay filtro de imagen o de código de barras,
    /// la API puede devolver menos de PageSize por página; por eso pedimos varias páginas API y acumulamos
    /// hasta llenar la página actual (respetando PageNumber y PageSize). Sin filtros, una sola llamada por página.</summary>
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
        _filter.FiltroCodigoBarra = _filtroCodigoBarraOption switch
        {
            "Con" => true,
            "Sin" => false,
            _ => null
        };
        try
        {
            _busquedaId++;
            bool tieneFiltroImagenOCodigo = _filter.FiltroImagen.HasValue || _filter.FiltroCodigoBarra.HasValue;

            if (tieneFiltroImagenOCodigo)
            {
                // Con filtros, la API puede devolver menos coincidencias por página; acumulamos páginas API
                // hasta tener al menos (paginaUsuario * PageSize) ítems que cumplan el filtro, luego tomamos la rebanada de la página actual.
                var paginaUsuario = _filter.PageNumber;
                var acumulada = new List<ProductoConImagenDto>();
                var apiPage = 1;
                const int maxPages = 100;
                var necesariosParaEstaPagina = paginaUsuario * _filter.PageSize;

                while (acumulada.Count < necesariosParaEstaPagina && apiPage <= maxPages)
                {
                    _filter.PageNumber = apiPage; // página que pedimos a la API en esta iteración
                    var chunk = (await ProductoQuery.GetProductosAsync(_filter, _token)).ToList();
                    if (chunk.Count == 0) break;

                    await CargarImagenesConTokenAsync(chunk);

                    // Aplicar filtros en cliente (por si la API no los aplica o devuelve mezclado)
                    var queCumplen = chunk.AsEnumerable();
                    if (_filter.FiltroImagen == true)
                        queCumplen = queCumplen.Where(p => !string.IsNullOrWhiteSpace(p.ImagenUrl));
                    else if (_filter.FiltroImagen == false)
                        queCumplen = queCumplen.Where(p => string.IsNullOrWhiteSpace(p.ImagenUrl));
                    if (_filter.FiltroCodigoBarra == true)
                        queCumplen = queCumplen.Where(p => !string.IsNullOrWhiteSpace(p.CodigoBarra));
                    else if (_filter.FiltroCodigoBarra == false)
                        queCumplen = queCumplen.Where(p => string.IsNullOrWhiteSpace(p.CodigoBarra));

                    acumulada.AddRange(queCumplen.ToList());
                    if (acumulada.Count >= necesariosParaEstaPagina) break;
                    if (chunk.Count < _filter.PageSize) break; // la API no tiene más
                    apiPage++;
                }

                // Rebanada correspondiente a la página del usuario (ej. página 2 y PageSize 25 → ítems 25..49)
                _items = acumulada
                    .Skip((paginaUsuario - 1) * _filter.PageSize)
                    .Take(_filter.PageSize)
                    .ToList();
                // Restaurar en el filtro la página que ve el usuario (no la última apiPage usada internamente)
                _filter.PageNumber = paginaUsuario;
            }
            else
            {
                // Sin filtros de imagen/código: una llamada por página, la API devuelve hasta PageSize ítems
                _items = (await ProductoQuery.GetProductosAsync(_filter, _token)).ToList();
                await CargarImagenesConTokenAsync(_items);
            }

            // Con filtros "Todos" no aplicamos filtro en cliente; la API ya devolvió la página.
            if (!tieneFiltroImagenOCodigo)
            {
                if (_filtroImagenOption == "Con")
                    _items = _items.Where(p => !string.IsNullOrWhiteSpace(p.ImagenUrl)).ToList();
                else if (_filtroImagenOption == "Sin")
                    _items = _items.Where(p => string.IsNullOrWhiteSpace(p.ImagenUrl)).ToList();
                if (_filter.FiltroCodigoBarra == true)
                    _items = _items.Where(p => !string.IsNullOrWhiteSpace(p.CodigoBarra)).ToList();
                else if (_filter.FiltroCodigoBarra == false)
                    _items = _items.Where(p => string.IsNullOrWhiteSpace(p.CodigoBarra)).ToList();
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
        _filtroCodigoBarraOption = "Todos";
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

    /// <summary>Abre el modal de búsqueda web para el producto indicado. Crea un estado nuevo (no reutiliza el anterior) para evitar asignar al producto equivocado.</summary>
    private async Task BuscarImagenWebAsync(ProductoConImagenDto? p)
    {
        if (p == null) return;
        var keysRaw = (_googleSearchKeysRaw ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (keysRaw.Count == 0)
        {
            _error = "Para buscar imágenes en la web, configurá una o más API keys de Google (Custom Search) en el filtro \"API key(s) Google (búsqueda imágenes)\".";
            await InvokeAsync(StateHasChanged);
            return;
        }
        _error = null;
        // Nuevo estado por producto: así el 2º y 3er producto no comparten estado con el anterior (SOLID: estado inmutable por sesión de modal).
        _busquedaWeb = new BusquedaWebState
        {
            ProductoID = p.ProductoID,
            DescripcionLarga = p.DescripcionLarga,
            CodigoBarra = p.CodigoBarra,
            Loading = true,
            Descargando = false,
            Error = null,
            Urls = null,
            SourceLabel = "Buscando en Google"
        };
        await InvokeAsync(StateHasChanged);
        try
        {
            var query = $"{p.CodigoBarra!.Trim()} | {p.DescripcionLarga!.Trim()}";
            var urls = await GoogleImageSearch.SearchImageUrlsAsync(query, keysRaw);
            if (_busquedaWeb != null)
            {
                _busquedaWeb.Urls = urls.Count > 0 ? new List<string>(urls) : null;
                _busquedaWeb.Loading = false;
                if (urls.Count == 0)
                    _busquedaWeb.Error = "No se encontraron imágenes. Probá con otras API keys o revisá el motor de búsqueda (cx).";
                else
                    await LogBusquedaWebUrlsAsync(urls);
            }
        }
        catch (Exception ex)
        {
            if (_busquedaWeb != null)
            {
                _busquedaWeb.Error = ex.Message;
                _busquedaWeb.Loading = false;
            }
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Abre el modal de búsqueda web usando SerpAPI (Google Images, ubicación Argentina). Misma lógica que BuscarImagenWebAsync pero con SerpAPI.</summary>
    private async Task BuscarImagenSerpApiAsync(ProductoConImagenDto? p)
    {
        if (p == null) return;
        if (string.IsNullOrWhiteSpace(_serpApiKeyRaw?.Trim()))
        {
            _error = "Para buscar imágenes con SerpAPI, configurá la API key de SerpAPI en el filtro \"API key SerpAPI\".";
            await InvokeAsync(StateHasChanged);
            return;
        }
        if (string.IsNullOrWhiteSpace(p.CodigoBarra) || string.IsNullOrWhiteSpace(p.DescripcionLarga))
        {
            _error = "El producto debe tener descripción y código de barras para buscar con SerpAPI.";
            await InvokeAsync(StateHasChanged);
            return;
        }
        _error = null;
        _busquedaWeb = new BusquedaWebState
        {
            ProductoID = p.ProductoID,
            DescripcionLarga = p.DescripcionLarga,
            CodigoBarra = p.CodigoBarra,
            Loading = true,
            Descargando = false,
            Error = null,
            Urls = null,
            SourceLabel = "Buscando con SerpAPI"
        };
        await InvokeAsync(StateHasChanged);
        try
        {
            var query = $"{p.CodigoBarra.Trim()} {p.DescripcionLarga.Trim()}";
            var urls = await SerpApiImageSearch.SearchImageUrlsAsync(query, _serpApiKeyRaw.Trim(), "Argentina");
            if (_busquedaWeb != null)
            {
                _busquedaWeb.Urls = urls.Count > 0 ? new List<string>(urls) : null;
                _busquedaWeb.Loading = false;
                if (urls.Count == 0)
                    _busquedaWeb.Error = "No se encontraron imágenes con SerpAPI. Revisá la API key o probá con otro producto.";
                else
                    await LogBusquedaWebUrlsAsync(urls);
            }
        }
        catch (Exception ex)
        {
            if (_busquedaWeb != null)
            {
                _busquedaWeb.Error = ex.Message;
                _busquedaWeb.Loading = false;
            }
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private void CerrarBusquedaWeb()
    {
        _busquedaWebCts?.Cancel();
        _busquedaWebCts?.Dispose();
        _busquedaWebCts = null;
        _busquedaWeb = null;
        StateHasChanged();
    }

    private void OnBusquedaWebKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            CerrarBusquedaWeb();
    }

    private async Task LogBusquedaWebUrlsAsync(IReadOnlyList<string> urls)
    {
        try
        {
            var list = urls.Select((u, i) => new { indice = i, url = u.Length > 80 ? u.Substring(0, 80) + "…" : u, urlCompletaLongitud = u.Length }).ToList();
            await JS.InvokeVoidAsync("__logAsignarImagenes", "Búsqueda web: URLs recibidas", JsonSerializer.Serialize(new { total = urls.Count, urls = list }));
        }
        catch { }
    }

    /// <summary>Proxies CORS para descargar imágenes desde el navegador. Si uno falla se prueba el siguiente.</summary>
    private static readonly string[] CorsProxyBases = new[]
    {
        "https://api.allorigins.win/raw?url=",
        "https://corsproxy.io/?url="
    };
    private const string CorsProxyBase = "https://api.allorigins.win/raw?url=";

    private async Task LogImagenWebFallida(string url, int indice)
    {
        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "Imagen búsqueda web FALLÓ al cargar (onerror)", JsonSerializer.Serialize(new
            {
                indice,
                urlPreview = url.Length > 100 ? url.Substring(0, 100) + "…" : url,
                urlLongitud = url.Length,
                posibleCausa = "CORS, 403 (hotlink), 404 o red. Intentando mostrar vía proxy CORS."
            }));
        }
        catch { }

        if (_busquedaWeb?.Urls == null || indice < 0 || indice >= _busquedaWeb.Urls.Count) return;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        _busquedaWeb.Urls[indice] = CorsProxyBase + Uri.EscapeDataString(url);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Asigna la imagen descargada al producto. Usa tiempo límite (12 s) y CancellationToken para no quedar colgado en "Descargando imagen" y poder elegir otra.</summary>
    private async Task SeleccionarImagenWebAsync(string imageUrl, int productId)
    {
        if (_busquedaWeb == null || productId == 0) return;
        if (_busquedaWeb.Descargando) return;
        if (!_busquedaWeb.EsParaProducto(productId)) return;

        _busquedaWebCts?.Cancel();
        _busquedaWebCts?.Dispose();
        _busquedaWebCts = new CancellationTokenSource();

        _busquedaWeb.Descargando = true;
        _busquedaWeb.Error = null;
        await InvokeAsync(StateHasChanged);
        try
        {
            var ct = _busquedaWebCts.Token;
            string? dataUrl = null;
            var yaEsProxy = imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && CorsProxyBases.Any(b => imageUrl.StartsWith(b, StringComparison.OrdinalIgnoreCase));
            if (yaEsProxy)
            {
                dataUrl = await GoogleImageSearch.FetchImageAsDataUrlAsync(imageUrl, ct);
            }
            else if (imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var encoded = Uri.EscapeDataString(imageUrl);
                var t1 = GoogleImageSearch.FetchImageAsDataUrlAsync(CorsProxyBases[0] + encoded, ct);
                var t2 = GoogleImageSearch.FetchImageAsDataUrlAsync(CorsProxyBases[1] + encoded, ct);
                var primera = await Task.WhenAny(t1, t2);
                dataUrl = await primera;
                if (string.IsNullOrWhiteSpace(dataUrl))
                    dataUrl = await (primera == t1 ? t2 : t1);
            }
            else
            {
                dataUrl = await GoogleImageSearch.FetchImageAsDataUrlAsync(imageUrl, ct);
            }

            if (!string.IsNullOrWhiteSpace(dataUrl) && dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var item = _items.FirstOrDefault(x => x.ProductoID == productId);
                if (item != null)
                {
                    item.ImagenUrl = dataUrl;
                    item.ImagenCargada = true;
                    CerrarBusquedaWeb();
                }
                else
                {
                    _busquedaWeb.Error = "Producto no encontrado en la lista actual. Cerrando y recargando podés solucionarlo.";
                }
            }
            else
            {
                _busquedaWeb.Error = "No se pudo descargar esta imagen. Probá con otra miniatura o con otro producto.";
            }
        }
        catch (OperationCanceledException)
        {
            _busquedaWeb.Error = "Descarga cancelada. Elegí otra imagen.";
        }
        finally
        {
            _busquedaWebCts?.Dispose();
            _busquedaWebCts = null;
            if (_busquedaWeb != null)
                _busquedaWeb.Descargando = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Cancela la descarga en curso para poder elegir otra imagen sin esperar.</summary>
    private void CancelarDescargaImagen()
    {
        _busquedaWebCts?.Cancel();
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
