using BlazorApp_ProductosAPI.Components;
using BlazorApp_ProductosAPI.Components.AsignarImagenes;
using BlazorApp_ProductosAPI.Models;
using BlazorApp_ProductosAPI.Models.AsignarImagenes;
using BlazorApp_ProductosAPI.Services;
using System.Text.Json;
using System.Text;
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
    [Inject] private ISerpApiImageSearchService SerpApiImageSearch { get; set; } = null!; // legado SerpAPI
    /// <summary>Servicio de patch de producto para guardar imagen y/u observaciones en la API.</summary>
    [Inject] private Services.AsignarImagenes.IProductoPatchService ProductoPatch { get; set; } = null!;
    [Inject] private ISerpApiOrganicSearchService SerpApiOrganicSearch { get; set; } = null!;
    /// <summary>Servicio de búsqueda de imágenes usando la API propia /Integration/ImageSearch.</summary>
    [Inject] private IIntegrationImageSearchService IntegrationImageSearch { get; set; } = null!;

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
    private bool _guardandoImagenModal;
    /// <summary>True = se muestra la modal de vista en grande de la imagen del producto (Nano Banana); Escape la cierra.</summary>
    private bool _imagenModalPreviewAbierto;
    private const string LsGeminiKey = "img_gkey";
    private const string LsGoogleSearchKeys = "google_search_keys";
    private const string LsSerpApiKey = "serpapi_key";

    private string _googleSearchKeysRaw = "";
    /// <summary>API key de SerpAPI para búsqueda de imágenes (Google Images, ubicación Argentina).</summary>
    private string _serpApiKeyRaw = "";
    /// <summary>Estado del modal "Buscar imagen en la web". Null = cerrado. Evita referencias obsoletas al 2º/3er producto (SOLID: estado único).</summary>
    private BusquedaWebState? _busquedaWeb;
    /// <summary>Si está asignado, se muestra una modal encima con la imagen en grande (Escape cierra solo esta y vuelve a la de SerpAPI).</summary>
    private string? _busquedaWebPreviewUrl;
    /// <summary>Cancelación de la descarga actual para no quedar en "Descargando imagen" y poder elegir otra.</summary>
    private CancellationTokenSource? _busquedaWebCts;
    /// <summary>Si true, en el próximo OnAfterRender se fuerza el valor del input de búsqueda desde JS para que respete código de barra + descripción completa.</summary>
    private bool _busquedaWebInputNeedsSync;

    /// <summary>Modal Observaciones: producto seleccionado y texto (RTF o plano).</summary>
    private ProductoConImagenDto? _productoObservaciones;
    private string _observacionesRtf = "";
    private bool _generandoObservaciones;
    private bool _guardandoObservaciones;
    private string? _errorObservaciones;
    /// <summary>True = pestaña Código RTF, False = pestaña Vista previa.</summary>
    private bool _observacionesVistaCodigo = false;
    /// <summary>Cuando es true, en OnAfterRender se rellena el contenteditable con el HTML del RTF (al abrir modal o al pasar a Vista previa).</summary>
    private bool _observacionesPreviewNeedsSyncFromRtf = false;
    /// <summary>Si true, el próximo blur del contenteditable no debe pisar _observacionesRtf (evita que el blur al hacer clic en Generar borre el resultado).</summary>
    private bool _observacionesSkipNextSyncFromPreview = false;

    /// <summary>Mensaje de notificación (toast) para operaciones de guardado (éxito / error).</summary>
    private string? _toastMessage;
    private bool _toastIsError;

    /// <summary>Vista actual: "Grid" (tarjetas) o "Lista" (tabla con selección y acciones masivas).</summary>
    private string _vistaModo = "Grid";
    /// <summary>IDs de productos seleccionados en la vista lista (para búsqueda/generación masiva).</summary>
    private HashSet<int> _seleccionados = new();
    /// <summary>Búsqueda masiva SerpAPI en curso.</summary>
    private bool _bulkSearching;
    /// <summary>Índice actual (1-based) y total al buscar imágenes masivamente. Ej.: 5 de 25.</summary>
    private int _bulkSearchingCurrent;
    private int _bulkSearchingTotal;
    /// <summary>Generación masiva con Gemini en curso.</summary>
    private bool _bulkGenerating;
    /// <summary>Índice actual (1-based) y total al generar imágenes masivamente.</summary>
    private int _bulkGeneratingCurrent;
    private int _bulkGeneratingTotal;
    /// <summary>Generación masiva de observaciones en curso.</summary>
    private bool _bulkGeneratingObservaciones;
    private int _bulkGeneratingObservacionesCurrent;
    private int _bulkGeneratingObservacionesTotal;
    /// <summary>Guardado masivo de cambios pendientes en curso (PATCH uno a uno).</summary>
    private bool _guardandoTodos;
    /// <summary>Modal de solo vista: imagen en grande al hacer clic en miniatura en la lista (sin edición).</summary>
    private string? _previewSoloImagenUrl;
    private string? _previewSoloImagenDesc;

    /// <summary>Últimas URLs de búsqueda por producto (API integración). Al abrir "Buscar imagen (API)" de nuevo, se muestran desde acá si existen.</summary>
    private readonly Dictionary<int, List<string>> _urlsBusquedaCachePorProducto = new();

    private bool _tieneImagenEnModal => !string.IsNullOrWhiteSpace(_imagenModalDataUrl) && !_imagenModalDataUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase);

    /// <summary>Cantidad de productos con imagen pendiente de guardar en la API (solo en memoria).</summary>
    private int CantidadPendienteGuardar => _items.Count(p => p.ImagenPendienteGuardar);
    /// <summary>Cantidad de productos con observaciones pendientes de guardar.</summary>
    private int CantidadObservacionesPendienteGuardar => _items.Count(p => p.ObservacionesPendienteGuardar);

    /// <summary>True = filtros colapsados después de buscar (solo se muestra el botón para expandir).</summary>
    private bool _filtersCollapsed;

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
            _seleccionados.Clear(); // Nueva búsqueda: limpia selección en vista lista (SOLID: estado coherente).
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
            if (_items.Count > 0)
                _filtersCollapsed = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ToggleFiltros() => _filtersCollapsed = !_filtersCollapsed;

    /// <summary>Cambia la vista a Grid (tarjetas).</summary>
    private void SetVistaGrid() { _vistaModo = "Grid"; StateHasChanged(); }

    /// <summary>Cambia la vista a Lista (tabla con selección y acciones masivas).</summary>
    private void SetVistaLista() { _vistaModo = "Lista"; StateHasChanged(); }

    private bool PuedeSiguiente => _items.Count == _filter.PageSize;

    /// <summary>True cuando hay más de una página o estamos en una página &gt; 1; entonces mostramos la barra de paginación.</summary>
    private bool TienePaginacion => _items.Count > 0 && (_filter.PageNumber > 1 || PuedeSiguiente);

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
        _imagenModalPreviewAbierto = false;
        _mejorandoImagen = false;
        StateHasChanged();
    }

    private void OnModalKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key != "Escape") return;
        if (_imagenModalPreviewAbierto)
        {
            CerrarPreviewImagenModal();
            return;
        }
        CerrarModal();
    }

    private void CerrarPreviewImagenModal()
    {
        _imagenModalPreviewAbierto = false;
        StateHasChanged();
    }

    private void AbrirPreviewImagenModal()
    {
        if (string.IsNullOrWhiteSpace(_imagenModalDataUrl) || _imagenModalDataUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase)) return;
        _imagenModalPreviewAbierto = true;
        StateHasChanged();
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

    /// <summary>Guarda en memoria la imagen del modal (Ver/Mejorar). No llama a la API; el usuario confirma con "Guardar cambios" en la página.</summary>
    private async Task GuardarCambiosModal()
    {
        if (_productoModal == null) return;
        if (string.IsNullOrWhiteSpace(_imagenModalDataUrl) || _imagenModalDataUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase))
            return;

        var item = _items.FirstOrDefault(x => x.ProductoID == _productoModal.ProductoID);
        if (item == null) return;

        item.ImagenUrl = _imagenModalDataUrl;
        item.ImagenCargada = true;
        item.ImagenPendienteGuardar = true;

        await MostrarToastAsync("Imagen guardada en memoria. Usá «Guardar cambios» para enviar a la API.", false);
        CerrarModal();
        await InvokeAsync(StateHasChanged);
    }

    private async Task AbrirModalObservacionesAsync(ProductoConImagenDto? p)
    {
        if (p == null) return;
        _productoObservaciones = p;
        _observacionesRtf = p.Observaciones ?? "";
        _errorObservaciones = null;
        _generandoObservaciones = false;
        _geminiApiKeyModal = await LocalStorage.GetItemAsync(LsGeminiKey) ?? "";
        _observacionesVistaCodigo = false; // por defecto mostrar Vista previa
        _observacionesPreviewNeedsSyncFromRtf = true;
        await InvokeAsync(StateHasChanged);
    }

    private void CerrarModalObservaciones()
    {
        _productoObservaciones = null;
        _observacionesRtf = "";
        _errorObservaciones = null;
        _generandoObservaciones = false;
        _guardandoObservaciones = false;
        _observacionesVistaCodigo = true;
        StateHasChanged();
    }

    /// <summary>HTML generado desde el RTF para la vista previa (negritas, viñetas, etc.). ToHtml no lanza y sanea la entrada.</summary>
    private string ObservacionesPreviewHtml => RtfToHtmlConverter.ToHtml(_observacionesRtf);

    /// <summary>Quita caracteres nulos y sustitutos que rompen la vista previa cuando el RTF viene de generación masiva.</summary>
    private static string SanitizeRtfForPreview(string? rtf)
    {
        if (string.IsNullOrEmpty(rtf)) return rtf ?? "";
        var sb = new StringBuilder(rtf.Length);
        foreach (var c in rtf)
        {
            if (c == '\0') continue;
            if (char.IsSurrogate(c)) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Normaliza el RTF para enviarlo a la API: cualquier carácter no ASCII se convierte a secuencias RTF estándar
    /// (\'xx para Latin‑1 y \uNNN? para el resto). De esta forma el texto viaja sólo con ASCII y
    /// evita problemas de codificación (acentos/ñ) al guardar y leer desde otras aplicaciones (WinForms, etc.).
    /// No modifica comandos RTF ya existentes como \'f3 o \u241?.
    /// </summary>
    private static string NormalizeRtfForApi(string? rtf)
    {
        if (string.IsNullOrEmpty(rtf)) return rtf ?? "";
        var sb = new StringBuilder(rtf.Length * 2);
        foreach (var c in rtf)
        {
            var code = (int)c;
            if (code <= 0x7F)
            {
                sb.Append(c);
            }
            else if (code >= 0x80 && code <= 0xFF)
            {
                sb.Append("\\'").Append(code.ToString("x2"));
            }
            else
            {
                sb.Append("\\u").Append(code).Append('?');
            }
        }
        return sb.ToString();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Sincronizar input de búsqueda del modal: forzar valor en DOM para que no se trunque (código de barra + descripción completa).
        if (_busquedaWebInputNeedsSync && _busquedaWeb != null)
        {
            _busquedaWebInputNeedsSync = false;
            try
            {
                await JS.InvokeVoidAsync("__setBusquedaWebQueryInputValue", _busquedaWeb.TextoBusqueda ?? "");
            }
            catch { /* no crítico */ }
        }

        // No usar __setObservacionesPreviewContent aquí: Blazor ya renderiza el contenido del contenteditable.
        // Si se reemplazaba con JS, en el siguiente diff Blazor intentaba removeChild sobre nodos ya eliminados -> "Cannot read properties of null (reading 'removeChild')".
        if (_productoObservaciones != null && _observacionesPreviewNeedsSyncFromRtf)
            _observacionesPreviewNeedsSyncFromRtf = false;

        if (!string.IsNullOrWhiteSpace(_previewSoloImagenUrl))
        {
            try
            {
                await JS.InvokeVoidAsync("eval", "var el = document.getElementById('preview-solo-imagen-overlay'); if (el) { el.focus(); }");
            }
            catch { }
        }
        if (_productoObservaciones != null)
        {
            try
            {
                await JS.InvokeVoidAsync("eval", "var el = document.getElementById('modal-observaciones-overlay'); if (el) { el.focus(); }");
            }
            catch { }
        }
    }

    /// <summary>Sincroniza el contenido del contenteditable (HTML) al RTF al perder foco. No sincroniza si está generando con IA o si se acaba de generar (evita pisar el resultado). Nunca propaga excepciones para evitar "An unhandled error has occurred".</summary>
    private async Task SyncPreviewToRtfAsync()
    {
        if (_productoObservaciones == null || _observacionesVistaCodigo || _generandoObservaciones) return;
        try
        {
            var html = await JS.InvokeAsync<string>("__getObservacionesPreviewContent", "observaciones-preview-editable");
            if (_generandoObservaciones) return;
            if (_observacionesSkipNextSyncFromPreview) { _observacionesSkipNextSyncFromPreview = false; return; }
            if (html != null)
                _observacionesRtf = HtmlToRtfConverter.ToRtf(html);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // Ignorar: JS (elemento no existe), serialización, o cualquier fallo al convertir HTML→RTF
        }
    }

    private void OnModalObservacionesKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            CerrarModalObservaciones();
    }

    /// <summary>Genera observaciones en RTF usando SerpAPI (búsqueda orgánica) + Gemini. Usa la misma API key de Gemini.</summary>
    private async Task GenerarObservacionesConIAAsync()
    {
        if (_productoObservaciones == null) return;
        var productId = _productoObservaciones.ProductoID;
        try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarObservacionesModal INICIO", JsonSerializer.Serialize(new { productId })); } catch { }
        var apiKeyGemini = _geminiApiKeyModal?.Trim();
        if (string.IsNullOrWhiteSpace(apiKeyGemini))
        {
            _errorObservaciones = "Ingresá la API key de Gemini en el cuadro de arriba.";
            return;
        }
        if (string.IsNullOrWhiteSpace(_serpApiKeyRaw?.Trim()))
        {
            _errorObservaciones = "Para generar con IA necesitás la API key de SerpAPI (en Filtros). SerpAPI busca información en Google y Gemini la convierte en observaciones RTF.";
            return;
        }
        var desc = _productoObservaciones.DescripcionLarga ?? "";
        var barra = _productoObservaciones.CodigoBarra ?? "";
        if (string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(barra))
        {
            _errorObservaciones = "El producto debe tener descripción o código de barras para buscar información.";
            return;
        }
        _errorObservaciones = null;
        _generandoObservaciones = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            if (_productoObservaciones == null) { _generandoObservaciones = false; await InvokeAsync(StateHasChanged); return; }
            var query = $"{barra} {desc}".Trim();
            IReadOnlyList<string> snippets;
            try
            {
                snippets = await SerpApiOrganicSearch.GetOrganicSnippetsAsync(query, _serpApiKeyRaw.Trim(), maxSnippets: 10);
            }
            catch (Exception ex)
            {
                try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarObservacionesModal SerpAPI ERROR", JsonSerializer.Serialize(new { productId, mensaje = ex.Message })); } catch { }
                _errorObservaciones = "SerpAPI: " + ex.Message;
                _generandoObservaciones = false;
                await InvokeAsync(StateHasChanged);
                return;
            }
            if (_productoObservaciones == null) { _generandoObservaciones = false; await InvokeAsync(StateHasChanged); return; }
            try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarObservacionesModal SerpAPI OK", JsonSerializer.Serialize(new { productId, snippetsCount = snippets?.Count ?? 0 })); } catch { }
            var snippetsText = snippets != null && snippets.Count > 0
                ? string.Join("\n\n", snippets.Select(s => "- " + s))
                : "(No se encontraron resultados de búsqueda para este producto.)";
            var prompt = $@"Eres un asistente que escribe observaciones de productos en formato RTF (Rich Text Format).
Producto: {desc}. Código de barras: {barra}.

Información obtenida de búsqueda en internet:
{snippetsText}

Escribe observaciones útiles para catálogo (descripción, uso, características) en RTF válido. Responde ÚNICAMENTE con el contenido RTF, sin explicaciones ni markdown. El RTF debe empezar por {{\rtf1 y usar comandos estándar como \par para párrafos. Para acentos y la letra ñ en español usa secuencias Unicode RTF: \u243? para ó, \u241? para ñ, \u225? para á, \u233? para é, \u237? para í, \u250? para ú, \u252? para ü (siempre seguido del signo ?). Escribe el texto en español correcto con todos los acentos y la ñ. Si no hay información útil, escribe un párrafo breve con la descripción del producto en RTF.";
            var result = await GeminiService.GenerateTextAsync(prompt, apiKeyGemini);
            if (_productoObservaciones == null) { _generandoObservaciones = false; await InvokeAsync(StateHasChanged); return; }
            if (result != null && result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                var raw = result.Text.Trim();
                if (raw.StartsWith("```", StringComparison.Ordinal))
                {
                    var first = raw.IndexOf('\n');
                    var last = raw.LastIndexOf("```", StringComparison.Ordinal);
                    if (first >= 0 && last > first + 1)
                        raw = raw.Substring(first + 1, last - first - 1).Trim();
                }
                // Normalizar acentos/ñ a secuencias RTF estándar antes de guardar/enviar a la API.
                _observacionesRtf = NormalizeRtfForApi(raw);
                _observacionesPreviewNeedsSyncFromRtf = true;
                _observacionesSkipNextSyncFromPreview = true;
                try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarObservacionesModal Gemini OK", JsonSerializer.Serialize(new { productId, rtfLen = _observacionesRtf.Length })); } catch { }
                // La vista previa se actualiza con el re-render (StateHasChanged en finally); no usar __setObservacionesPreviewContent para evitar removeChild.
            }
            else
            {
                _errorObservaciones = result?.ErrorMessage ?? "Gemini no devolvió texto.";
                try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarObservacionesModal Gemini sin texto", JsonSerializer.Serialize(new { productId, error = _errorObservaciones })); } catch { }
            }
        }
        catch (Exception ex)
        {
            _errorObservaciones = ex.Message;
            try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarObservacionesModal EXCEPCIÓN", JsonSerializer.Serialize(new { productId, mensaje = ex.Message, stack = ex.StackTrace })); } catch { }
        }
        finally
        {
            _generandoObservaciones = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task MostrarToastAsync(string mensaje, bool esError)
    {
        _toastMessage = mensaje;
        _toastIsError = esError;
        var current = _toastMessage;
        await InvokeAsync(StateHasChanged);

        // Auto-cierre en ~3 segundos si el mensaje no cambió.
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            if (_toastMessage == current)
            {
                _toastMessage = null;
                await InvokeAsync(StateHasChanged);
            }
        });
    }

    private void CerrarToast()
    {
        _toastMessage = null;
        StateHasChanged();
    }

    private async Task GuardarObservacionesEnProductoAsync()
    {
        if (_productoObservaciones == null || string.IsNullOrWhiteSpace(_token))
        {
            CerrarModalObservaciones();
            return;
        }

        var texto = string.IsNullOrWhiteSpace(_observacionesRtf) ? null : NormalizeRtfForApi(_observacionesRtf);

        // Actualizar modelo en memoria para que la UI quede consistente inmediatamente.
        var item = _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID);
        if (item != null)
        {
            item.Observaciones = texto;
            item.ObservacionesPendienteGuardar = false; // Se guarda ya en la API desde este modal.
        }

        var request = new ProductoPatchRequest
        {
            CodigoID = _productoObservaciones.ProductoID,
            ImagenEspecified = false,
            Imagen = "null",
            ObservacionEspecified = true,
            Observacion = texto
        };

        _guardandoObservaciones = true;
        _errorObservaciones = null;
        await InvokeAsync(StateHasChanged);
        try
        {
            var result = await ProductoPatch.PatchProductoAsync(request, _token);
            if (!result.Success)
            {
                _errorObservaciones = "No se pudo guardar las observaciones en la API.";
                await MostrarToastAsync("No se pudo guardar las observaciones en la API.", true);
                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones ERROR", JsonSerializer.Serialize(new
                    {
                        request,
                        result.StatusCode,
                        result.ResponseBody,
                        result.ErrorMessage
                    }));
                }
                catch { }
                return;
            }
            await MostrarToastAsync("Observaciones guardadas correctamente.", false);
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones OK", JsonSerializer.Serialize(new
                {
                    request,
                    result.StatusCode,
                    result.ResponseBody
                }));
            }
            catch { }
            CerrarModalObservaciones();
        }
        catch (Exception ex)
        {
            _errorObservaciones = ex.Message;
            await MostrarToastAsync("Error guardando observaciones: " + ex.Message, true);
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones EXCEPCIÓN", JsonSerializer.Serialize(new { request, ex = ex.Message }));
            }
            catch { }
        }
        finally
        {
            _guardandoObservaciones = false;
            await InvokeAsync(StateHasChanged);
        }
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
        if (string.IsNullOrWhiteSpace(p.DescripcionLarga))
        {
            _error = "El producto debe tener descripción para buscar con SerpAPI.";
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
            SelectedImageUrl = null,
            SourceLabel = "Buscando con SerpAPI"
        };
        await InvokeAsync(StateHasChanged);
        try
        {
            var query = string.IsNullOrWhiteSpace(p.CodigoBarra)
                ? p.DescripcionLarga.Trim()
                : $"{p.CodigoBarra.Trim()} {p.DescripcionLarga.Trim()}";
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

    /// <summary>
    /// Abre el modal de búsqueda web usando la API propia /Integration/ImageSearch.
    /// Reutiliza el mismo estado y UI que SerpAPI, pero sin depender de API keys externas.
    /// </summary>
    private async Task BuscarImagenIntegrationAsync(ProductoConImagenDto? p)
    {
        if (p == null) return;
        if (string.IsNullOrWhiteSpace(_token))
        {
            _error = "Debes iniciar sesión para buscar imágenes.";
            await InvokeAsync(StateHasChanged);
            return;
        }
        if (string.IsNullOrWhiteSpace(p.DescripcionLarga))
        {
            _error = "El producto debe tener descripción para buscar imágenes.";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var barra = (p.CodigoBarra ?? "").Trim();
        var desc = (p.DescripcionLarga ?? "").Trim();
        // Query corta para la API y para el textbox: barra + primeras palabras (ej. "7791337601024 YOGURISIMO"), no la descripción completa.
        var queryInicialCorta = ImageSearchQueryHelper.ConstruirQueryInicialCorta(barra, desc);

        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "MODAL_QUERY_INICIAL",
                JsonSerializer.Serialize(new
                {
                    productoID = p.ProductoID,
                    codigoBarra = string.IsNullOrWhiteSpace(barra) ? "(vacío)" : barra,
                    descripcionLongitud = desc.Length,
                    queryEnviadaALaApi = queryInicialCorta,
                    mensaje = "Al abrir el modal se envía a la API exactamente esta query (barra + primeras palabras)."
                }));
        }
        catch { /* logging no crítico */ }

        _error = null;
        // Textbox muestra y envía a la API esta query corta; el usuario puede editarla y "Buscar de nuevo" enviará lo que escriba.
        var textoBusquedaInicial = queryInicialCorta;
        _busquedaWeb = new BusquedaWebState
        {
            ProductoID = p.ProductoID,
            DescripcionLarga = p.DescripcionLarga,
            CodigoBarra = p.CodigoBarra,
            TextoBusqueda = textoBusquedaInicial,
            PermiteEditarQuery = true,
            Loading = true,
            Descargando = false,
            Error = null,
            Urls = null,
            SelectedImageUrl = null,
            SourceLabel = "Buscando con API de imágenes"
        };
        _busquedaWebInputNeedsSync = true;
        await InvokeAsync(StateHasChanged);

        // Si ya tenemos URLs en caché de una búsqueda anterior (ej. búsqueda masiva en lista), mostrarlas sin llamar a la API.
        if (_urlsBusquedaCachePorProducto.TryGetValue(p.ProductoID, out var cached) && cached != null && cached.Count > 0)
        {
            _busquedaWeb.Urls = new List<string>(cached);
            _busquedaWeb.Loading = false;
            await LogBusquedaWebUrlsAsync(cached);
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            // Primera llamada: exactamente la query corta que está en el textbox (ej. "7791337601024 YOGURISIMO").
            IReadOnlyList<string>? urls = null;
            if (!string.IsNullOrWhiteSpace(queryInicialCorta))
            {
                urls = await IntegrationImageSearch.SearchImageUrlsAsync(queryInicialCorta, _token!);
            }
            // Si no hay resultados con la query corta, reintentar con descripción completa y luego abreviada.
            if ((urls == null || urls.Count == 0) && _busquedaWeb != null)
            {
                urls = await BuscarUrlsIntegracionPorProductoAsync(barra, desc);
            }
            if (_busquedaWeb != null)
            {
                _busquedaWeb.Urls = urls != null && urls.Count > 0 ? new List<string>(urls) : null;
                _busquedaWeb.Loading = false;
                if (urls != null && urls.Count > 0)
                {
                    _urlsBusquedaCachePorProducto[p.ProductoID] = new List<string>(urls);
                    _busquedaWeb.TextoBusqueda = textoBusquedaInicial;
                    await LogBusquedaWebUrlsAsync(urls);
                }
                else
                    _busquedaWeb.Error = "No se encontraron imágenes en la API de integración. Probá editando el texto y «Buscar de nuevo».";
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

    /// <summary>
    /// Busca URLs de imagen en la API de integración para un producto.
    /// Primera llamada: código de barra + descripción (o solo descripción si no hay barra), según regla DRR.
    /// Si no hay resultados y se usó barra+desc, reintenta solo con descripción.
    /// </summary>
    private async Task<IReadOnlyList<string>> BuscarUrlsIntegracionPorProductoAsync(string barra, string desc)
    {
        // Siempre código de barra + descripción (o solo descripción si no hay barra). Construcción explícita para lista/masivo y modal.
        var query = ImageSearchQueryHelper.ConstruirQuery(barra, desc);
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(_token))
            return Array.Empty<string>();

        // Garantía: si hay barra y desc, la query debe contener ambos (no solo barra).
        var barraTrim = (barra ?? "").Trim();
        var descTrim = (desc ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(barraTrim) && !string.IsNullOrWhiteSpace(descTrim) && !query.Contains(' '))
            query = $"{barraTrim} {descTrim}";

        // Log: query exacta que se envía a la API DRR (para "Buscar imagen (API) para seleccionados" y modal).
        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "API_IMAGEN QUERY_CONSTRUIDA",
                JsonSerializer.Serialize(new
                {
                    codigoBarra = string.IsNullOrWhiteSpace(barra) ? "(vacío)" : barra,
                    descripcion = descTrim.Length > 80 ? descTrim.Substring(0, 80) + "…" : descTrim,
                    queryEnviadaALaApi = query,
                    longitudQuery = query.Length,
                    mensaje = "Query enviada a la API: código de barra y descripción (o solo descripción si no hay barra)."
                }));
        }
        catch { }

        var tieneBarra = !string.IsNullOrWhiteSpace(barra);

        // Primera llamada: siempre según regla (barra+desc o solo desc).
        try
        {
            var urls = await IntegrationImageSearch.SearchImageUrlsAsync(query, _token!);
            if (urls != null && urls.Count > 0)
                return urls;
        }
        catch
        {
            // La API puede devolver status "error" en body con HTTP 200; cae aquí.
        }

        // Fallback: si usamos barra+desc y no hubo resultados, probar solo descripción (la API a veces solo responde a desc).
        if (tieneBarra && !string.IsNullOrWhiteSpace(desc))
        {
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenIntegration FALLBACK_SOLO_DESC",
                    JsonSerializer.Serialize(new { queryPrimera = query, queryFallback = desc.Trim(), mensaje = "Reintentando solo con descripción." }));
            }
            catch { }
            try
            {
                var urls = await IntegrationImageSearch.SearchImageUrlsAsync(desc.Trim(), _token!);
                if (urls != null && urls.Count > 0)
                    return urls;
            }
            catch
            {
                // Ignorar y devolver vacío al final.
            }
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Para "Buscar de nuevo" en el modal: usa exactamente el texto del textbox y, si no hay resultados,
    /// prueba fallbacks (solo descripción, descripción abreviada) según <see cref="ImageSearchQueryHelper.ObtenerQueriesFallbackParaTextoLibre"/>.
    /// Respeta lo que el usuario escribe en el textbox (leído desde el DOM).
    /// </summary>
    private async Task<IReadOnlyList<string>> BuscarUrlsIntegracionConFallbackTextoAsync(string texto)
    {
        var queries = ImageSearchQueryHelper.ObtenerQueriesFallbackParaTextoLibre(texto);
        foreach (var q in queries)
        {
            if (string.IsNullOrWhiteSpace(q))
                continue;
            try
            {
                var urls = await IntegrationImageSearch.SearchImageUrlsAsync(q, _token!);
                if (urls != null && urls.Count > 0)
                    return urls;
            }
            catch
            {
                // Siguiente variante sin propagar error.
            }
        }
        return Array.Empty<string>();
    }

    /// <summary>
    /// "Buscar de nuevo" en el modal: envía a la API DRR exactamente el texto del campo (código de barra y descripción).
    /// Se usa el valor enlazado <see cref="BusquedaWebState.TextoBusqueda"/> (actualizado por @bind en cada tecla); solo si está vacío se lee del DOM.
    /// Si no hay resultados, se prueban fallbacks en <see cref="BuscarUrlsIntegracionConFallbackTextoAsync"/>.
    /// </summary>
    private async Task BuscarDeNuevoIntegrationAsync()
    {
        if (_busquedaWeb == null || !_busquedaWeb.PermiteEditarQuery || string.IsNullOrWhiteSpace(_token)) return;
        if (_busquedaWeb.Loading) return;

        // Usar SIEMPRE el valor del modelo (TextoBusqueda): @bind lo actualiza en cada tecla, así respetamos lo que el usuario escribió.
        // Solo si está vacío, leer del DOM por si el binding no se aplicó.
        var texto = (_busquedaWeb.TextoBusqueda ?? "").Trim();
        if (string.IsNullOrWhiteSpace(texto))
        {
            try
            {
                var fromDom = await JS.InvokeAsync<string>("__getBusquedaWebQueryInputValue");
                texto = (fromDom ?? "").Trim();
            }
            catch { }
        }
        if (_busquedaWeb != null)
            _busquedaWeb.TextoBusqueda = texto;

        if (string.IsNullOrWhiteSpace(texto))
        {
            _busquedaWeb!.Error = "Escribí un texto de búsqueda (código de barra y descripción).";
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarDeNuevo TEXTO_ENVIADO",
                JsonSerializer.Serialize(new { longitud = texto.Length, textoCompleto = texto, mensaje = "Este es el texto exacto que se envía a la API." }));
        }
        catch { }

        var state = _busquedaWeb;
        state.Loading = true;
        state.Error = null;
        state.Urls = null;
        state.SelectedImageUrl = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            // Primera petición: exactamente el texto del input (código de barra + descripción). Si falla, fallback solo descripción.
            var urls = await BuscarUrlsIntegracionConFallbackTextoAsync(texto);
            if (state == _busquedaWeb && _busquedaWeb != null)
            {
                _busquedaWeb.Urls = urls != null && urls.Count > 0 ? new List<string>(urls) : null;
                if (urls == null || urls.Count == 0)
                    _busquedaWeb.Error = "No se encontraron imágenes con ese texto. Probá modificando la búsqueda.";
                else
                {
                    _urlsBusquedaCachePorProducto[_busquedaWeb.ProductoID] = new List<string>(urls);
                    await LogBusquedaWebUrlsAsync(urls);
                }
            }
        }
        catch (Exception ex)
        {
            if (state == _busquedaWeb && _busquedaWeb != null)
                _busquedaWeb.Error = ex.Message;
        }
        finally
        {
            if (state == _busquedaWeb && _busquedaWeb != null)
                _busquedaWeb.Loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Obtiene URLs de imagen para un producto usando la API de integración.
    /// Regla: con código de barra → "barra + descripción"; sin barra → solo descripción.
    /// </summary>
    private async Task<IReadOnlyList<string>> BuscarUrlsIntegracionAsync(ProductoConImagenDto p)
    {
        var barra = (p.CodigoBarra ?? "").Trim();
        var desc = (p.DescripcionLarga ?? "").Trim();
        var query = ImageSearchQueryHelper.ConstruirQuery(barra, desc);
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        try
        {
            var urls = await IntegrationImageSearch.SearchImageUrlsAsync(query, _token!);
            if (urls != null && urls.Count > 0)
            {
                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenIntegration QUERY_OK",
                        JsonSerializer.Serialize(new { p.ProductoID, p.Codigo, query, urlsCount = urls.Count }));
                }
                catch { }
                return urls;
            }
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenIntegration QUERY_SIN_URLS",
                    JsonSerializer.Serialize(new { p.ProductoID, p.Codigo, query }));
            }
            catch { }
            return urls ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenIntegration QUERY_ERROR",
                    JsonSerializer.Serialize(new { p.ProductoID, p.Codigo, query, error = ex.Message }));
            }
            catch { }
            throw;
        }
    }

    private void CerrarBusquedaWeb()
    {
        _busquedaWebCts?.Cancel();
        _busquedaWebCts?.Dispose();
        _busquedaWebCts = null;
        _busquedaWeb = null;
        _busquedaWebPreviewUrl = null;
        StateHasChanged();
    }

    private void CerrarPreviewImagenSerpApi()
    {
        _busquedaWebPreviewUrl = null;
        StateHasChanged();
    }

    private void OnBusquedaWebKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key != "Escape") return;
        if (!string.IsNullOrWhiteSpace(_busquedaWebPreviewUrl))
        {
            CerrarPreviewImagenSerpApi();
            return;
        }
        CerrarBusquedaWeb();
    }

    private void OnPreviewImagenKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            CerrarPreviewImagenSerpApi();
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
                    // Mostramos en UI la imagen tal cual viene (puede ser WebP, PNG, etc.).
                    item.ImagenUrl = dataUrl;
                    item.ImagenCargada = true;

                    if (!string.IsNullOrWhiteSpace(_token))
                    {
                        // SaveProductImageAsync se encarga de optimizar y convertir a JPEG antes de llamar al PATCH,
                        // así la API siempre recibe un formato soportado.
                        var ok = await ProductImage.SaveProductImageAsync(item.ProductoID, dataUrl, _token);
                        if (!ok)
                        {
                            _busquedaWeb.Error = "No se pudo guardar la imagen en la API.";
                            await MostrarToastAsync("No se pudo guardar la imagen en la API.", true);
                        }
                        else
                        {
                            await MostrarToastAsync("Imagen guardada correctamente.", false);
                            CerrarBusquedaWeb();
                        }
                    }
                    else
                    {
                        _busquedaWeb.Error = "No hay token de autenticación para guardar la imagen.";
                        await MostrarToastAsync("No hay token de autenticación para guardar la imagen.", true);
                    }
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

    /// <summary>Al hacer clic en una miniatura de SerpAPI se selecciona y se abre la modal de vista en grande (Escape cierra esa modal y vuelve a la de resultados).</summary>
    private void SeleccionarPreviewImagenSerpApi(string imageUrl)
    {
        if (_busquedaWeb == null) return;
        _busquedaWeb.SelectedImageUrl = imageUrl;
        _busquedaWeb.Error = null;
        _busquedaWebPreviewUrl = imageUrl;
        StateHasChanged();
    }

    /// <summary>Descarga la imagen seleccionada en el modal SerpAPI y la asigna al producto en memoria (ImagenPendienteGuardar = true). No llama a la API.</summary>
    private async Task GuardarImagenSeleccionadaSerpApiAsync()
    {
        if (_busquedaWeb == null || string.IsNullOrWhiteSpace(_busquedaWeb.SelectedImageUrl) || _busquedaWeb.ProductoID == 0) return;

        var imageUrl = _busquedaWeb.SelectedImageUrl;
        var productId = _busquedaWeb.ProductoID;

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
                dataUrl = await GoogleImageSearch.FetchImageAsDataUrlAsync(imageUrl, ct);
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
                dataUrl = await GoogleImageSearch.FetchImageAsDataUrlAsync(imageUrl, ct);

            if (!string.IsNullOrWhiteSpace(dataUrl) && dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var item = _items.FirstOrDefault(x => x.ProductoID == productId);
                if (item != null)
                {
                    item.ImagenUrl = dataUrl;
                    item.ImagenCargada = true;
                    item.ImagenPendienteGuardar = true;
                    await MostrarToastAsync("Imagen guardada en memoria. Usá «Guardar cambios» para enviar a la API.", false);
                    CerrarBusquedaWeb();
                }
                else
                {
                    _busquedaWeb.Error = "Producto no encontrado en la lista actual.";
                    await MostrarToastAsync("Producto no encontrado.", true);
                }
            }
            else
            {
                _busquedaWeb.Error = "No se pudo descargar esta imagen. Probá con otra.";
                await MostrarToastAsync("No se pudo descargar la imagen. Probá con otra.", true);
            }
        }
        catch (OperationCanceledException)
        {
            _busquedaWeb.Error = "Descarga cancelada.";
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
            else
                p.ImagenPendienteGuardar = false;
        }
        finally
        {
            _savingImageId = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ---------- Vista lista: selección y acciones masivas ----------

    /// <summary>Conmuta la selección de un producto en la vista lista.</summary>
    private void ToggleSeleccionAsync(ProductoConImagenDto p)
    {
        if (p == null) return;
        if (_seleccionados.Contains(p.ProductoID))
            _seleccionados.Remove(p.ProductoID);
        else
            _seleccionados.Add(p.ProductoID);
        StateHasChanged();
    }

    /// <summary>Selecciona todos o ninguno según el estado actual (vista lista).</summary>
    private void ToggleTodosAsync()
    {
        if (_items.Count == 0) return;
        var todosSeleccionados = _items.All(p => _seleccionados.Contains(p.ProductoID));
        if (todosSeleccionados)
            _seleccionados.Clear();
        else
            _seleccionados = _items.Select(p => p.ProductoID).ToHashSet();
        StateHasChanged();
    }

    /// <summary>Abre el modal de solo vista (imagen en grande) al hacer clic en la miniatura en la lista.</summary>
    private void AbrirPreviewSoloImagenAsync(ProductoConImagenDto? p)
    {
        if (p == null || string.IsNullOrWhiteSpace(p.ImagenUrl) || p.ImagenUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase))
            return;
        _previewSoloImagenUrl = p.ImagenUrl;
        _previewSoloImagenDesc = p.DescripcionLarga ?? p.Codigo ?? "—";
        StateHasChanged();
    }

    /// <summary>Cierra el modal de vista de imagen (miniatura en lista).</summary>
    private void CerrarPreviewSoloImagen()
    {
        _previewSoloImagenUrl = null;
        _previewSoloImagenDesc = null;
        StateHasChanged();
    }

    private void OnPreviewSoloImagenKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            CerrarPreviewSoloImagen();
    }

    /// <summary>Envía a la API (PATCH) todos los productos con imagen u observaciones pendientes, uno por uno.
    /// Imágenes: SaveProductImageAsync (reducción en cliente + PATCH). Observaciones: PATCH solo Observacion.</summary>
    private async Task GuardarTodosLosCambiosAsync()
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            await MostrarToastAsync("Debes iniciar sesión para guardar.", true);
            return;
        }
        var pendientesImagen = _items.Where(p => p.ImagenPendienteGuardar && !string.IsNullOrWhiteSpace(p.ImagenUrl)).ToList();
        var pendientesObservaciones = _items.Where(p => p.ObservacionesPendienteGuardar).ToList();
        if (pendientesImagen.Count == 0 && pendientesObservaciones.Count == 0)
        {
            await MostrarToastAsync("No hay cambios pendientes de guardar.", false);
            return;
        }
        _guardandoTodos = true;
        await InvokeAsync(StateHasChanged);
        var okImg = 0;
        var failImg = 0;
        var okObs = 0;
        var failObs = 0;
        try
        {
            for (int i = 0; i < pendientesImagen.Count; i++)
            {
                var item = pendientesImagen[i];
                var ok = await ProductImage.SaveProductImageAsync(item.ProductoID, item.ImagenUrl!, _token);
                if (ok) { item.ImagenPendienteGuardar = false; okImg++; } else failImg++;
                await InvokeAsync(StateHasChanged);
            }
            for (int i = 0; i < pendientesObservaciones.Count; i++)
            {
                var item = pendientesObservaciones[i];
                var request = new ProductoPatchRequest
                {
                    CodigoID = item.ProductoID,
                    ImagenEspecified = false,
                    Imagen = "null",
                    ObservacionEspecified = true,
                    // Siempre normalizamos el RTF antes de enviarlo a la API para que WinForms lo interprete igual.
                    Observacion = NormalizeRtfForApi(item.Observaciones)
                };
                try
                {
                    // Log detallado por ítem para diagnosticar por qué algunas observaciones no se guardan.
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones MASIVO ANTES", JsonSerializer.Serialize(new
                    {
                        indice = i + 1,
                        total = pendientesObservaciones.Count,
                        request.CodigoID,
                        rtfLen = request.Observacion?.Length ?? 0
                    }));
                }
                catch { }

                var result = await ProductoPatch.PatchProductoAsync(request, _token);
                if (result.Success)
                {
                    item.ObservacionesPendienteGuardar = false;
                    okObs++;
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones MASIVO OK", JsonSerializer.Serialize(new
                        {
                            indice = i + 1,
                            total = pendientesObservaciones.Count,
                            request.CodigoID,
                            statusCode = result.StatusCode
                        }));
                    }
                    catch { }
                }
                else
                {
                    failObs++;
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones MASIVO ERROR", JsonSerializer.Serialize(new
                        {
                            indice = i + 1,
                            total = pendientesObservaciones.Count,
                            request.CodigoID,
                            statusCode = result.StatusCode,
                            error = result.ErrorMessage,
                            body = result.ResponseBody
                        }));
                    }
                    catch { }
                }

                await InvokeAsync(StateHasChanged);
            }
            var msg = new List<string>();
            if (okImg > 0 || failImg > 0) msg.Add($"{okImg} imagen(es)" + (failImg > 0 ? $", {failImg} fallo(s)" : ""));
            if (okObs > 0 || failObs > 0) msg.Add($"{okObs} observación(es)" + (failObs > 0 ? $", {failObs} fallo(s)" : ""));
            if (msg.Count > 0)
                await MostrarToastAsync((failImg + failObs) == 0 ? "Guardado correctamente: " + string.Join("; ", msg) : "Guardado: " + string.Join("; ", msg), (failImg + failObs) > 0);
        }
        catch (Exception ex)
        {
            await MostrarToastAsync("Error guardando: " + ex.Message, true);
        }
        finally
        {
            _guardandoTodos = false;
            _seleccionados.Clear(); // Deseleccionar todo al finalizar guardado.
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Cantidad máxima de URLs a intentar descargar por producto en búsqueda masiva.
    /// Si la primera falla (CORS, red, etc.), se prueba la siguiente hasta asignar alguna imagen por producto.
    /// </summary>
    private const int MaxUrlsToTryPerProductoMasivo = 20;

    /// <summary>
    /// Intenta descargar una imagen desde una lista de URLs (SOLID: responsabilidad única).
    /// Prueba en orden hasta que una descarga correctamente o se agotan los intentos.
    /// </summary>
    /// <param name="productId">ID del producto (para logs).</param>
    /// <param name="codigo">Código del producto (para logs).</param>
    /// <param name="urls">Lista de URLs devueltas por la API de búsqueda.</param>
    /// <param name="indice">Índice 1-based del producto en el lote (para logs).</param>
    /// <param name="total">Total de productos en el lote (para logs).</param>
    /// <returns>Tupla (dataUrl, urlIndex): si se descargó una imagen, (dataUrl, índice 1-based de la URL usada); si no, (null, -1).</returns>
    private async Task<(string? dataUrl, int urlIndexUsed)> IntentarDescargarAlgunaImagenDeUrlsAsync(
        int productId,
        string? codigo,
        IReadOnlyList<string> urls,
        int indice,
        int total)
    {
        var toTry = Math.Min(MaxUrlsToTryPerProductoMasivo, urls.Count);
        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration INTENTO_DESCARGA",
                JsonSerializer.Serialize(new
                {
                    indice,
                    total,
                    productId,
                    codigo,
                    urlsDisponibles = urls.Count,
                    urlsAProbar = toTry,
                    mensaje = $"Se intentará descargar hasta {toTry} imagen(es) hasta asignar una al producto."
                }));
        }
        catch { }

        for (int u = 0; u < toTry; u++)
        {
            var imageUrl = urls[u];
            if (string.IsNullOrWhiteSpace(imageUrl)) continue;

            try
            {
                var dataUrl = await GoogleImageSearch.FetchImageAsDataUrlAsync(imageUrl);
                if (!string.IsNullOrWhiteSpace(dataUrl) && dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration FETCH_OK",
                            JsonSerializer.Serialize(new { indice, total, productId, codigo, urlIndex = u + 1, totalUrlsProbadas = u + 1 }));
                    }
                    catch { }
                    return (dataUrl, u + 1);
                }
            }
            catch (Exception exFetch)
            {
                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration ERROR_FETCH",
                        JsonSerializer.Serialize(new
                        {
                            indice,
                            total,
                            productId,
                            codigo,
                            urlIndex = u + 1,
                            error = exFetch.Message,
                            imageUrlPreview = imageUrl.Length > 100 ? imageUrl.Substring(0, 100) + "…" : imageUrl
                        }));
                }
                catch { }
            }
        }

        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration FALLA_TODAS_URLS",
                JsonSerializer.Serialize(new
                {
                    indice,
                    total,
                    productId,
                    codigo,
                    urlsIntentadas = toTry,
                    mensaje = $"No se pudo descargar ninguna de las {toTry} URL(s) probadas para este producto."
                }));
        }
        catch { }
        return (null, -1);
    }

    /// <summary>
    /// Asignación masiva de imágenes: para cada producto seleccionado busca en la API DRR (código de barra + descripción,
    /// o solo descripción si no tiene código de barra). Si no encuentra imágenes, reintenta con descripción abreviada (8, 5, 3, 2 palabras).
    /// Descarga la primera imagen válida y la asigna en memoria (ImagenPendienteGuardar); el usuario guarda con «Guardar cambios».
    /// </summary>
    private async Task BuscarImagenMasivoSerpApiAsync()
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            await MostrarToastAsync("Debes iniciar sesión para buscar imágenes.", true);
            return;
        }

        var idsToProcess = _items
            .Where(p => _seleccionados.Contains(p.ProductoID) && !string.IsNullOrWhiteSpace(p.DescripcionLarga))
            .Select(p => p.ProductoID)
            .ToList();

        if (idsToProcess.Count == 0)
        {
            await MostrarToastAsync("Seleccioná al menos un producto con descripción.", false);
            return;
        }

        // Log de inicio (diagnóstico)
        try
        {
            var seleccionadosInfo = _items
                .Where(p => idsToProcess.Contains(p.ProductoID))
                .Select(p => new
                {
                    p.ProductoID,
                    p.Codigo,
                    desc = p.DescripcionLarga,
                    barra = p.CodigoBarra
                })
                .ToList();
            await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration INICIO",
                JsonSerializer.Serialize(new
                {
                    totalSeleccionados = idsToProcess.Count,
                    ids = idsToProcess,
                    seleccionados = seleccionadosInfo
                }));
        }
        catch { }

        _error = null;
        _bulkSearching = true;
        _bulkSearchingTotal = idsToProcess.Count;
        _bulkSearchingCurrent = 0;
        await InvokeAsync(StateHasChanged);

        var okCount = 0;
        var total = idsToProcess.Count;
        var idsConImagen = new List<int>();
        var idsSinImagen = new List<object>(); // { productId, codigo, motivo }

        try
        {
            for (int i = 0; i < total; i++)
            {
                // Resolver siempre desde _items para que la vista lista muestre las actualizaciones (misma referencia que Items).
                var productId = idsToProcess[i];
                var p = _items.FirstOrDefault(x => x.ProductoID == productId);
                if (p == null)
                {
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration SKIP",
                            JsonSerializer.Serialize(new { indice = i + 1, total, productId = idsToProcess[i], motivo = "producto no encontrado en _items" }));
                    }
                    catch { }
                    idsSinImagen.Add(new { productId, codigo = (string?)null, motivo = "no encontrado" });
                    _bulkSearchingCurrent = i + 1;
                    await InvokeAsync(StateHasChanged);
                    continue;
                }

                var desc = (p.DescripcionLarga ?? "").Trim();
                var barra = (p.CodigoBarra ?? "").Trim();
                // Query para API DRR: siempre código de barra + descripción (o solo descripción si no hay barra).
                var queryPrimera = ImageSearchQueryHelper.ConstruirQuery(barra, desc);
                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration QUERY_POR_PRODUCTO",
                        JsonSerializer.Serialize(new { productId = p.ProductoID, codigo = p.Codigo, barra, descCorta = desc.Length > 50 ? desc.Substring(0, 50) + "…" : desc, queryEnviadaALaApi = queryPrimera }));
                }
                catch { }
                // Variantes: descripción completa y luego abreviada (8, 5, 3, 2 palabras) por si la API no responde a la completa.
                var variantesDesc = ImageSearchQueryHelper.ObtenerVariantesDescripcion(desc);
                if (variantesDesc.Count == 0)
                {
                    idsSinImagen.Add(new { productId = p.ProductoID, codigo = p.Codigo, motivo = "descripción vacía" });
                    _bulkSearchingCurrent = i + 1;
                    await InvokeAsync(StateHasChanged);
                    continue;
                }

                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration PROCESANDO",
                        JsonSerializer.Serialize(new
                        {
                            indice = i + 1,
                            total,
                            productId = p.ProductoID,
                            codigo = p.Codigo,
                            variantesDescripcion = variantesDesc.Count,
                            tieneBarra = !string.IsNullOrWhiteSpace(barra),
                            descCorta = desc.Length > 40 ? desc.Substring(0, 40) + "…" : desc
                        }));
                }
                catch { }

                string? dataUrlAsignada = null;
                int urlIndexUsada = -1;
                IReadOnlyList<string>? urlsFinales = null;
                var intentoVariante = 0;

                foreach (var descVariante in variantesDesc)
                {
                    intentoVariante++;
                    if (intentoVariante > 1)
                    {
                        try
                        {
                            await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration REINTENTO_DESC_ACORTADA",
                                JsonSerializer.Serialize(new
                                {
                                    indice = i + 1,
                                    total,
                                    productId = p.ProductoID,
                                    codigo = p.Codigo,
                                    intento = intentoVariante,
                                    descVariante = descVariante.Length > 60 ? descVariante.Substring(0, 60) + "…" : descVariante,
                                    mensaje = "Reintentando búsqueda con descripción acortada para obtener otras URLs."
                                }));
                        }
                        catch { }
                    }

                    IReadOnlyList<string> urls;
                    try
                    {
                        urls = await BuscarUrlsIntegracionPorProductoAsync(barra, descVariante);
                    }
                    catch (Exception exApi)
                    {
                        try
                        {
                            await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration ERROR_API",
                                JsonSerializer.Serialize(new { indice = i + 1, total, p.ProductoID, p.Codigo, descVariante = descVariante.Length > 40 ? descVariante.Substring(0, 40) + "…" : descVariante, error = exApi.Message }));
                        }
                        catch { }
                        continue; // Siguiente variante.
                    }

                    if (urls == null || urls.Count == 0)
                    {
                        try
                        {
                            await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration SIN_URLS",
                                JsonSerializer.Serialize(new { indice = i + 1, total, p.ProductoID, p.Codigo, descVariante = descVariante.Length > 40 ? descVariante.Substring(0, 40) + "…" : descVariante }));
                        }
                        catch { }
                        continue;
                    }

                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration URLs_OBTENIDAS",
                            JsonSerializer.Serialize(new { indice = i + 1, total, p.ProductoID, p.Codigo, urlsCount = urls.Count, intentoVariante, primeraUrl = urls[0] }));
                    }
                    catch { }

                    _urlsBusquedaCachePorProducto[p.ProductoID] = new List<string>(urls);
                    var (dataUrl, urlIndex) = await IntentarDescargarAlgunaImagenDeUrlsAsync(p.ProductoID, p.Codigo, urls, i + 1, total);

                    if (!string.IsNullOrWhiteSpace(dataUrl))
                    {
                        dataUrlAsignada = dataUrl;
                        urlIndexUsada = urlIndex;
                        urlsFinales = urls;
                        break; // Éxito con esta variante.
                    }
                }

                if (!string.IsNullOrWhiteSpace(dataUrlAsignada) && urlsFinales != null)
                {
                    p.ImagenUrl = dataUrlAsignada;
                    p.ImagenCargada = true;
                    p.ImagenPendienteGuardar = true;
                    okCount++;
                    idsConImagen.Add(p.ProductoID);
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration ASIGNACIÓN_OK",
                            JsonSerializer.Serialize(new
                            {
                                indice = i + 1,
                                total,
                                productId = p.ProductoID,
                                codigo = p.Codigo,
                                urlIndexUsada = urlIndexUsada,
                                urlsDisponibles = urlsFinales.Count,
                                intentoVariante,
                                mensaje = $"Imagen asignada (URL #{urlIndexUsada} de {urlsFinales.Count}, variante #{intentoVariante})."
                            }));
                    }
                    catch { }
                }
                else
                {
                    var motivoFalla = $"Se probaron {variantesDesc.Count} variante(s) de descripción y ninguna permitió descargar una imagen.";
                    idsSinImagen.Add(new { productId = p.ProductoID, codigo = p.Codigo, motivo = motivoFalla });
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration FALLA_ITEM",
                            JsonSerializer.Serialize(new
                            {
                                indice = i + 1,
                                total,
                                p.ProductoID,
                                p.Codigo,
                                variantesProbadas = variantesDesc.Count,
                                motivo = motivoFalla
                            }));
                    }
                    catch { }
                }

                _bulkSearchingCurrent = i + 1;
                await InvokeAsync(StateHasChanged);
            }

            // Log resumen detallado
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration RESUMEN",
                    JsonSerializer.Serialize(new
                    {
                        totalProcesados = total,
                        okCount,
                        idsConImagen,
                        idsSinImagen,
                        mensaje = $"{okCount} de {total} producto(s) con imagen asignada. Con imagen: [{string.Join(", ", idsConImagen)}]. Sin imagen: {idsSinImagen.Count} (ver idsSinImagen en este log)."
                    }));
            }
            catch { }

            await MostrarToastAsync(
                okCount > 0
                    ? $"Se asignaron {okCount} imagen(es) en memoria. Usá «Guardar cambios»."
                    : "No se pudo asignar ninguna imagen con la API de integración.",
                okCount == 0);
        }
        finally
        {
            _bulkSearching = false;
            _bulkSearchingCurrent = 0;
            _bulkSearchingTotal = 0;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Para cada producto seleccionado, genera una imagen con Gemini y la asigna en memoria (ImagenPendienteGuardar = true).
    /// Usa lista fija de IDs; hasta 3 intentos por producto (reintentos con pausa) para respetar la cantidad seleccionada.</summary>
    private async Task GenerarImagenMasivoAsync()
    {
        var apiKey = _geminiApiKeyModal?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = await LocalStorage.GetItemAsync(LsGeminiKey);
        if (string.IsNullOrWhiteSpace(apiKey?.Trim()))
        {
            await MostrarToastAsync("Configurá la API key de Gemini (abrí «Ver / Mejorar» en un producto y guardala).", true);
            return;
        }
        // Lista fija de IDs a procesar: así se respeta exactamente la cantidad seleccionada aunque haya re-renders.
        var idsToProcess = _items.Where(p => _seleccionados.Contains(p.ProductoID)).Select(p => p.ProductoID).ToList();
        if (idsToProcess.Count == 0)
        {
            await MostrarToastAsync("Seleccioná al menos un producto.", false);
            return;
        }
        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarImagenMasivo INICIO", JsonSerializer.Serialize(new
            {
                totalSeleccionados = idsToProcess.Count,
                ids = idsToProcess
            }));
        }
        catch { }
        _bulkGenerating = true;
        _bulkGeneratingTotal = idsToProcess.Count;
        _bulkGeneratingCurrent = 0;
        await InvokeAsync(StateHasChanged);
        var okCount = 0;
        var total = idsToProcess.Count;
        var fallidos = new List<object>();
        try
        {
            for (int i = 0; i < total; i++)
            {
                var productId = idsToProcess[i];
                var p = _items.FirstOrDefault(x => x.ProductoID == productId);
                if (p == null)
                {
                    try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarImagenMasivo SKIP (no encontrado)", JsonSerializer.Serialize(new { indice = i + 1, total, productId })); } catch { }
                    _bulkGeneratingCurrent = i + 1;
                    await InvokeAsync(StateHasChanged);
                    continue;
                }
                var descCorta = (p.DescripcionLarga ?? "—").Length > 50 ? (p.DescripcionLarga ?? "—").Substring(0, 50) + "…" : (p.DescripcionLarga ?? "—");
                var prompt = $"Crea una imagen de producto profesional para catálogo. Fondo blanco o neutro, iluminación de estudio. Producto: {p.DescripcionLarga ?? "—"}. Código: {p.Codigo ?? "—"}. Código de barra: {p.CodigoBarra ?? "—"}. La imagen debe ser realista, solo el producto, estilo fotografía comercial.";
                var result = await GeminiService.ImproveOrCreateProductImageAsync(prompt, apiKey!, null, null);
                var intento = 1;
                if (result == null || !result.Success || result.ImageBytes == null || result.ImageBytes.Length == 0)
                {
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarImagenMasivo intento 1 falló", JsonSerializer.Serialize(new
                        {
                            indice = i + 1,
                            total,
                            productId,
                            descCorta,
                            resultNull = result == null,
                            success = result?.Success ?? false,
                            errorMessage = result?.ErrorMessage ?? "(null)",
                            imageBytesLength = result?.ImageBytes?.Length ?? 0
                        }));
                    }
                    catch { }
                    await Task.Delay(1500);
                    result = await GeminiService.ImproveOrCreateProductImageAsync(prompt, apiKey!, null, null);
                    intento = 2;
                }
                if (result == null || !result.Success || result.ImageBytes == null || result.ImageBytes.Length == 0)
                {
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarImagenMasivo intento 2 falló", JsonSerializer.Serialize(new
                        {
                            indice = i + 1,
                            total,
                            productId,
                            descCorta,
                            resultNull = result == null,
                            success = result?.Success ?? false,
                            errorMessage = result?.ErrorMessage ?? "(null)",
                            imageBytesLength = result?.ImageBytes?.Length ?? 0
                        }));
                    }
                    catch { }
                    await Task.Delay(2500);
                    result = await GeminiService.ImproveOrCreateProductImageAsync(prompt, apiKey!, null, null);
                    intento = 3;
                }
                if (result != null && result.Success && result.ImageBytes != null && result.ImageBytes.Length > 0)
                {
                    var mimeOut = result.MimeType ?? "image/png";
                    p.ImagenUrl = "data:" + mimeOut + ";base64," + Convert.ToBase64String(result.ImageBytes);
                    p.ImagenCargada = true;
                    p.ImagenPendienteGuardar = true;
                    okCount++;
                    try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarImagenMasivo OK", JsonSerializer.Serialize(new { indice = i + 1, total, productId, descCorta, intento, imageBytesLength = result.ImageBytes.Length })); } catch { }
                }
                else
                {
                    fallidos.Add(new { productId, descCorta, errorMessage = result?.ErrorMessage ?? "(resultado nulo o sin imagen)" });
                    try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarImagenMasivo FALLÓ (3 intentos)", JsonSerializer.Serialize(new { indice = i + 1, total, productId, descCorta, errorMessage = result?.ErrorMessage ?? "(null)" })); } catch { }
                }
                _bulkGeneratingCurrent = i + 1;
                await InvokeAsync(StateHasChanged);
            }
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarImagenMasivo RESUMEN", JsonSerializer.Serialize(new { totalProcesados = total, okCount, fallidosCount = fallidos.Count, fallidos }));
            }
            catch { }
            await MostrarToastAsync(okCount > 0 ? $"Se generaron {okCount} imagen(es) en memoria. Usá «Guardar cambios»." : "No se pudo generar ninguna imagen.", okCount == 0);
        }
        finally
        {
            _bulkGenerating = false;
            _bulkGeneratingCurrent = 0;
            _bulkGeneratingTotal = 0;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Genera observaciones con IA (SerpAPI + Gemini) para cada producto seleccionado y las deja en memoria (ObservacionesPendienteGuardar = true).</summary>
    private async Task GenerarObservacionesMasivoAsync()
    {
        var apiKeyGemini = _geminiApiKeyModal?.Trim();
        if (string.IsNullOrWhiteSpace(apiKeyGemini))
            apiKeyGemini = await LocalStorage.GetItemAsync(LsGeminiKey);
        if (string.IsNullOrWhiteSpace(apiKeyGemini?.Trim()))
        {
            await MostrarToastAsync("Configurá la API key de Gemini (abrí «Observaciones» en un producto y guardala).", true);
            return;
        }
        if (string.IsNullOrWhiteSpace(_serpApiKeyRaw?.Trim()))
        {
            await MostrarToastAsync("Configurá la API key de SerpAPI en Filtros para generar observaciones.", true);
            return;
        }
        var idsToProcess = _items.Where(p => _seleccionados.Contains(p.ProductoID) && (!string.IsNullOrWhiteSpace(p.DescripcionLarga) || !string.IsNullOrWhiteSpace(p.CodigoBarra))).Select(p => p.ProductoID).ToList();
        if (idsToProcess.Count == 0)
        {
            await MostrarToastAsync("Seleccioná al menos un producto con descripción o código de barras.", false);
            return;
        }
        _bulkGeneratingObservaciones = true;
        _bulkGeneratingObservacionesTotal = idsToProcess.Count;
        _bulkGeneratingObservacionesCurrent = 0;
        await InvokeAsync(StateHasChanged);
        var okCount = 0;
        var total = idsToProcess.Count;
        try
        {
            for (int i = 0; i < total; i++)
            {
                var p = _items.FirstOrDefault(x => x.ProductoID == idsToProcess[i]);
                if (p == null) { _bulkGeneratingObservacionesCurrent = i + 1; await InvokeAsync(StateHasChanged); continue; }
                var desc = p.DescripcionLarga ?? "";
                var barra = p.CodigoBarra ?? "";
                var query = $"{barra} {desc}".Trim();
                IReadOnlyList<string> snippets;
                try
                {
                    snippets = await SerpApiOrganicSearch.GetOrganicSnippetsAsync(query, _serpApiKeyRaw.Trim(), maxSnippets: 10);
                }
                catch
                {
                    _bulkGeneratingObservacionesCurrent = i + 1;
                    await InvokeAsync(StateHasChanged);
                    continue;
                }
                var snippetsText = snippets.Count > 0 ? string.Join("\n\n", snippets.Select(s => "- " + s)) : "(No se encontraron resultados de búsqueda para este producto.)";
                var prompt = $@"Eres un asistente que escribe observaciones de productos en formato RTF (Rich Text Format).
Producto: {desc}. Código de barras: {barra}.

Información obtenida de búsqueda en internet:
{snippetsText}

Escribe observaciones útiles para catálogo (descripción, uso, características) en RTF válido. Responde ÚNICAMENTE con el contenido RTF, sin explicaciones ni markdown. El RTF debe empezar por {{\rtf1 y usar comandos estándar como \par para párrafos. Para acentos y la letra ñ en español usa secuencias Unicode RTF: \u243? para ó, \u241? para ñ, \u225? para á, \u233? para é, \u237? para í, \u250? para ú, \u252? para ü (siempre seguido del signo ?). Escribe el texto en español correcto con todos los acentos y la ñ. Si no hay información útil, escribe un párrafo breve con la descripción del producto en RTF.";
                var result = await GeminiService.GenerateTextAsync(prompt, apiKeyGemini!);
                if (result != null && result.Success && !string.IsNullOrWhiteSpace(result.Text))
                {
                    var rtf = result.Text.Trim();
                    if (rtf.StartsWith("```", StringComparison.Ordinal))
                    {
                        var first = rtf.IndexOf('\n');
                        var last = rtf.LastIndexOf("```", StringComparison.Ordinal);
                        if (first >= 0 && last > first + 1)
                            rtf = rtf.Substring(first + 1, last - first - 1).Trim();
                    }
                    // Normalizar a RTF ASCII‑safe para que la API y WinForms respeten los acentos.
                    var normalizedRtf = NormalizeRtfForApi(rtf);
                    p.Observaciones = SanitizeRtfForPreview(normalizedRtf);
                    p.ObservacionesPendienteGuardar = true;
                    okCount++;
                    try { await JS.InvokeVoidAsync("__logAsignarImagenes", "GenerarObservacionesMasivo item OK", JsonSerializer.Serialize(new { indice = i + 1, total, productId = p.ProductoID, rtfLen = p.Observaciones?.Length ?? 0 })); } catch { }
                }
                _bulkGeneratingObservacionesCurrent = i + 1;
                await InvokeAsync(StateHasChanged);
            }
            await MostrarToastAsync(okCount > 0 ? $"Se generaron {okCount} observación(es) en memoria. Usá «Guardar cambios»." : "No se pudo generar ninguna observación.", okCount == 0);
        }
        finally
        {
            _bulkGeneratingObservaciones = false;
            _bulkGeneratingObservacionesCurrent = 0;
            _bulkGeneratingObservacionesTotal = 0;
            await InvokeAsync(StateHasChanged);
        }
    }
}
