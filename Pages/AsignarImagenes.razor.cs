using BlazorApp_ProductosAPI.Components;
using BlazorApp_ProductosAPI.Components.AsignarImagenes;
using BlazorApp_ProductosAPI.Models;
using BlazorApp_ProductosAPI.Models.AsignarImagenes;
using BlazorApp_ProductosAPI.Services;
using System.Diagnostics;
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
    /// <summary>Servicio de generación de texto con OpenAI (Chat Completions) para observaciones RTF.</summary>
    [Inject] private IOpenAITextService OpenAITextService { get; set; } = null!;
    /// <summary>Servicio de búsqueda de imágenes usando la API propia /Integration/ImageSearch.</summary>
    [Inject] private IIntegrationImageSearchService IntegrationImageSearch { get; set; } = null!;

    private const string PlaceholderSvg = "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22200%22 height=%22200%22 viewBox=%220 0 200 200%22%3E%3Crect fill=%22%23e9ecef%22 width=%22200%22 height=%22200%22/%3E%3Ctext x=%22100%22 y=%22105%22 text-anchor=%22middle%22 fill=%22%23999%22 font-size=%2214%22%3ESin imagen%3C/text%3E%3C/svg%3E";

    private ProductoQueryFilter _filter = new();
    private string _filtroImagenOption = "Todos";
    private string _filtroCodigoBarraOption = "Todos";
    private string _filtroObservacionOption = "Todos";
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
    private const string LsOpenAiKey = "openai_key";

    private string _googleSearchKeysRaw = "";
    /// <summary>API key de SerpAPI para búsqueda de imágenes (Google Images, ubicación Argentina).</summary>
    private string _serpApiKeyRaw = "";
    /// <summary>API key de OpenAI para generación de observaciones (Chat Completions).</summary>
    private string _openAiKey = "";
    /// <summary>Estado del modal "Buscar imagen en la web". Null = cerrado. Evita referencias obsoletas al 2º/3er producto (SOLID: estado único).</summary>
    private BusquedaWebState? _busquedaWeb;
    /// <summary>Si está asignado, se muestra una modal encima con la imagen en grande (Escape cierra solo esta y vuelve a la de SerpAPI).</summary>
    private string? _busquedaWebPreviewUrl;
    /// <summary>Cancelación de la descarga actual para no quedar en "Descargando imagen" y poder elegir otra.</summary>
    private CancellationTokenSource? _busquedaWebCts;
    /// <summary>Causa de la última cancelación (para el LOG cuando aparece "Descarga cancelada").</summary>
    private string? _busquedaWebCancelacionRazon;
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
    /// <summary>Texto adicional opcional que el usuario puede agregar al prompt por defecto de OpenAI para observaciones.</summary>
    private string _observacionesPromptExtra = "";
    /// <summary>Descripción corta generada por IA (editable en modal Observaciones).</summary>
    private string? _descripcionCortaGenerada;
    /// <summary>Descripción larga generada por IA (editable en modal Observaciones).</summary>
    private string? _descripcionLargaGenerada;

    /// <summary>Modal de opciones antes de generar con OpenAI: abreviar descripción corta, autocompletar descripción larga, editar prompts.</summary>
    private bool _showModalOpcionesGeneracion;
    /// <summary>Instrucciones adicionales para OpenAI: colapsado por defecto; al hacer clic se expande.</summary>
    private bool _showInstruccionesOpenAI;
    /// <summary>True si la generación es masiva (lista); false si es un solo producto (modal Observaciones).</summary>
    private bool _opcionesGeneracionEsMasivo;
    private bool _opcionesAbreviarCorta = true;
    private bool _opcionesAutocompletarLarga = true;
    private bool _editandoPromptAbreviar;
    private bool _editandoPromptLarga;
    private string _promptAbreviarCorta = "";
    private string _promptDescripcionLarga = "";

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
    /// <summary>Cantidad de productos con observaciones y/o descripciones (corta/larga) pendientes de guardar.</summary>
    private int CantidadObservacionesPendienteGuardar => _items.Count(p => p.ObservacionesPendienteGuardar || p.DescripcionCortaPendienteGuardar || p.DescripcionLargaPendienteGuardar);

    /// <summary>True = filtros colapsados después de buscar (solo se muestra el botón para expandir).</summary>
    private bool _filtersCollapsed;

    /// <summary>Si falta API key de OpenAI o de Gemini, se muestra la modal una vez al entrar (se guardan en LocalStorage).</summary>
    private bool _showApiKeysModal;
    private string _apiKeysModalGemini = "";
    private string _apiKeysModalOpenAi = "";

    protected override async Task OnInitializedAsync()
    {
        await CargarTokenYCatalogos();
        _googleSearchKeysRaw = await LocalStorage.GetItemAsync(LsGoogleSearchKeys) ?? "";
        _serpApiKeyRaw = await LocalStorage.GetItemAsync(LsSerpApiKey) ?? "";
        _geminiApiKeyModal = await LocalStorage.GetItemAsync(LsGeminiKey) ?? "";
        _openAiKey = await LocalStorage.GetItemAsync(LsOpenAiKey) ?? "";
        var faltaGemini = string.IsNullOrWhiteSpace(_geminiApiKeyModal);
        var faltaOpenAi = string.IsNullOrWhiteSpace(_openAiKey);
        if (!string.IsNullOrWhiteSpace(_token) && (faltaGemini || faltaOpenAi))
        {
            _showApiKeysModal = true;
            _apiKeysModalGemini = _geminiApiKeyModal ?? "";
            _apiKeysModalOpenAi = _openAiKey ?? "";
        }
    }

    private async Task GuardarApiKeysModalAsync()
    {
        if (!string.IsNullOrWhiteSpace(_apiKeysModalGemini))
        {
            _geminiApiKeyModal = _apiKeysModalGemini.Trim();
            await LocalStorage.SetItemAsync(LsGeminiKey, _geminiApiKeyModal);
        }
        if (!string.IsNullOrWhiteSpace(_apiKeysModalOpenAi))
        {
            _openAiKey = _apiKeysModalOpenAi.Trim();
            await LocalStorage.SetItemAsync(LsOpenAiKey, _openAiKey);
        }
        _showApiKeysModal = false;
        await InvokeAsync(StateHasChanged);
    }

    private void CerrarApiKeysModal()
    {
        _showApiKeysModal = false;
        StateHasChanged();
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

    /// <summary>Copia el filtro actual con otro PageSize; usado para la API cuando hay filtro de imagen sin tocar _filter.PageSize.</summary>
    private static ProductoQueryFilter CloneFilterWithPageSize(ProductoQueryFilter f, int pageSize)
    {
        return new ProductoQueryFilter
        {
            PageSize = pageSize,
            PageNumber = f.PageNumber,
            CodigoBarra = f.CodigoBarra,
            DescripcionLarga = f.DescripcionLarga,
            FamiliaID = f.FamiliaID,
            MarcaID = f.MarcaID,
            SucursalID = f.SucursalID,
            FechaModifDesde = f.FechaModifDesde,
            FechaModifHasta = f.FechaModifHasta,
            FiltroImagen = f.FiltroImagen,
            FiltroCodigoBarra = f.FiltroCodigoBarra,
            FiltroObservacion = f.FiltroObservacion
        };
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
        _filter.FiltroObservacion = _filtroObservacionOption switch
        {
            "Con" => true,
            "Sin" => false,
            _ => null
        };
        try
        {
            var swTotal = Stopwatch.StartNew();
            _busquedaId++;
            _seleccionados.Clear(); // Nueva búsqueda: limpia selección en vista lista (SOLID: estado coherente).
            bool tieneFiltroImagen = _filter.FiltroImagen.HasValue;
            bool tieneFiltroCodigo = _filter.FiltroCodigoBarra.HasValue;
            bool tieneFiltroObservacion = _filter.FiltroObservacion.HasValue;

            // Cuando hay filtro de imagen o de observación, la API no filtra por esos criterios (Include=2 trae todos).
            // Hay que pedir varias páginas a la API, filtrar en cliente y acumular hasta llenar la página pedida.
            if (tieneFiltroImagen || tieneFiltroObservacion)
            {
                var paginaUsuario = _filter.PageNumber;
                var pageSizeUsuario = _filter.PageSize;
                var acumulada = new List<ProductoConImagenDto>();
                var apiPage = 1;
                const int maxPages = 10;
                const int pageSizeApi = 100;
                var necesarios = paginaUsuario * pageSizeUsuario;

                var filtroApi = CloneFilterWithPageSize(_filter, pageSizeApi);
                while (acumulada.Count < necesarios && apiPage <= maxPages)
                {
                    filtroApi.PageNumber = apiPage;
                    var chunk = (await ProductoQuery.GetProductosAsync(filtroApi, _token)).ToList();
                    if (chunk.Count == 0) break;

                    var queCumplen = chunk.AsEnumerable();
                    if (tieneFiltroImagen)
                    {
                        if (filtroApi.FiltroImagen == true)
                            queCumplen = queCumplen.Where(p => !string.IsNullOrWhiteSpace(p.ImagenUrl));
                        else
                            queCumplen = queCumplen.Where(p => string.IsNullOrWhiteSpace(p.ImagenUrl));
                    }
                    if (tieneFiltroCodigo)
                    {
                        if (filtroApi.FiltroCodigoBarra == true)
                            queCumplen = queCumplen.Where(p => !string.IsNullOrWhiteSpace(p.CodigoBarra));
                        else
                            queCumplen = queCumplen.Where(p => string.IsNullOrWhiteSpace(p.CodigoBarra));
                    }
                    if (tieneFiltroObservacion)
                    {
                        if (filtroApi.FiltroObservacion == true)
                            queCumplen = queCumplen.Where(p => p.Observaciones != null);
                        else
                            queCumplen = queCumplen.Where(p => p.Observaciones == null);
                    }

                    acumulada.AddRange(queCumplen.ToList());
                    if (acumulada.Count >= necesarios) break;
                    if (chunk.Count < pageSizeApi) break;
                    apiPage++;
                }

                _items = acumulada
                    .Skip((paginaUsuario - 1) * pageSizeUsuario)
                    .Take(pageSizeUsuario)
                    .ToList();
                _filter.PageNumber = paginaUsuario;
            }
            else
            {
                // Sin filtro de imagen ni observación: una sola llamada (filtro código lo aplica la API).
                _items = (await ProductoQuery.GetProductosAsync(_filter, _token)).ToList();
                if (tieneFiltroCodigo)
                {
                    if (_filter.FiltroCodigoBarra == true)
                        _items = _items.Where(p => !string.IsNullOrWhiteSpace(p.CodigoBarra)).ToList();
                    else
                        _items = _items.Where(p => string.IsNullOrWhiteSpace(p.CodigoBarra)).ToList();
                }
            }

            // Cargar imágenes en segundo plano (no bloquear la lista): la grilla/tabla se muestra al instante
            _ = CargarImagenesConTokenAsync(_items);

            // Si no usamos el bucle (una sola llamada), aplicar en cliente por imagen/código/observación por si acaso.
            if (!tieneFiltroImagen && !tieneFiltroObservacion)
            {
                if (_filtroImagenOption == "Con")
                    _items = _items.Where(p => !string.IsNullOrWhiteSpace(p.ImagenUrl)).ToList();
                else if (_filtroImagenOption == "Sin")
                    _items = _items.Where(p => string.IsNullOrWhiteSpace(p.ImagenUrl)).ToList();
                if (_filter.FiltroCodigoBarra == true)
                    _items = _items.Where(p => !string.IsNullOrWhiteSpace(p.CodigoBarra)).ToList();
                else if (_filter.FiltroCodigoBarra == false)
                    _items = _items.Where(p => string.IsNullOrWhiteSpace(p.CodigoBarra)).ToList();
                if (_filter.FiltroObservacion == true)
                    _items = _items.Where(p => p.Observaciones != null).ToList();
                else if (_filter.FiltroObservacion == false)
                    _items = _items.Where(p => p.Observaciones == null).ToList();
            }

            swTotal.Stop();
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "Búsqueda productos TIEMPO TOTAL", JsonSerializer.Serialize(new
                {
                    tiempoMs = swTotal.ElapsedMilliseconds,
                    tiempoSeg = Math.Round(swTotal.Elapsed.TotalSeconds, 2),
                    itemsMostrados = _items.Count,
                    filtroImagen = tieneFiltroImagen,
                    filtroCodigo = tieneFiltroCodigo,
                    filtroObservacion = tieneFiltroObservacion
                }));
            }
            catch { }
            _ = LogProductosAlConsolaAsync(); // No esperar: solo log en F12; la lista se muestra al instante
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
        _filtroObservacionOption = "Todos";
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
        _observacionesPromptExtra = "";
        _showInstruccionesOpenAI = false;
        _descripcionCortaGenerada = p.DescripcionCorta;
        _descripcionLargaGenerada = p.DescripcionLarga;
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
        _descripcionCortaGenerada = null;
        _descripcionLargaGenerada = null;
        _errorObservaciones = null;
        _generandoObservaciones = false;
        _guardandoObservaciones = false;
        _observacionesVistaCodigo = true;
        StateHasChanged();
    }

    /// <summary>Abre la modal de opciones antes de generar con OpenAI (un producto o masivo).</summary>
    private void AbrirModalOpcionesGeneracion(bool esMasivo)
    {
        _opcionesGeneracionEsMasivo = esMasivo;
        if (string.IsNullOrWhiteSpace(_promptAbreviarCorta))
            _promptAbreviarCorta = GetPromptAbreviarCortaDefault();
        if (string.IsNullOrWhiteSpace(_promptDescripcionLarga))
            _promptDescripcionLarga = GetPromptDescripcionLargaDefault();
        _showModalOpcionesGeneracion = true;
        StateHasChanged();
    }

    private void CerrarModalOpcionesGeneracion()
    {
        _showModalOpcionesGeneracion = false;
        _editandoPromptAbreviar = false;
        _editandoPromptLarga = false;
        StateHasChanged();
    }

    /// <summary>Prompt por defecto: descripción corta = solo el nombre del producto abreviado (lo que nosotros llamamos descripción).</summary>
    private static string GetPromptAbreviarCortaDefault()
    {
        return "Genera ÚNICAMENTE el nombre del producto abreviado (máximo 30 caracteres). Es solo el nombre, acortado para listados o etiquetas. No agregues características, ni uso, ni párrafos: solo el nombre del producto. Sin RTF, sin código de barras. Responde solo con ese nombre abreviado.";
    }

    /// <summary>Prompt por defecto: descripción larga = solo el nombre del producto extendido (lo que nosotros llamamos descripción).</summary>
    private static string GetPromptDescripcionLargaDefault()
    {
        return "Genera ÚNICAMENTE el nombre del producto en su forma extendida o completa. Es solo el nombre (lo que nosotros llamamos descripción): puede incluir variante, presentación o tamaño si aplica, pero sin párrafos, sin características ni texto de marketing. Sin RTF, sin código de barras. Responde solo con ese nombre extendido.";
    }

    private void OnModalOpcionesGeneracionKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Escape") CerrarModalOpcionesGeneracion();
    }

    /// <summary>Al editar la descripción larga en la modal, actualiza el estado y el ítem en _items para que quede en memoria (al cerrar y reabrir se vea igual).</summary>
    private void OnDescripcionLargaInput(ChangeEventArgs e)
    {
        _descripcionLargaGenerada = e.Value?.ToString();
        var item = _productoObservaciones != null ? _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID) : null;
        if (item != null)
        {
            item.DescripcionLarga = _descripcionLargaGenerada;
            item.DescripcionLargaPendienteGuardar = true;
        }
        StateHasChanged();
    }

    /// <summary>Al editar la descripción corta en la modal, actualiza el estado y el ítem en _items para que quede en memoria (al cerrar y reabrir se vea igual).</summary>
    private void OnDescripcionCortaInput(ChangeEventArgs e)
    {
        _descripcionCortaGenerada = e.Value?.ToString();
        var item = _productoObservaciones != null ? _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID) : null;
        if (item != null)
        {
            item.DescripcionCorta = _descripcionCortaGenerada;
            item.DescripcionCortaPendienteGuardar = true;
        }
        StateHasChanged();
    }

    /// <summary>True si el producto actual tiene descripción corta o larga pendiente de guardar (editadas a mano o generadas con OpenAI).</summary>
    private bool TieneDescripcionesPendientesGuardar
    {
        get
        {
            if (_productoObservaciones == null) return false;
            var item = _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID);
            return item != null && (item.DescripcionCortaPendienteGuardar || item.DescripcionLargaPendienteGuardar);
        }
    }

    /// <summary>True si el producto actual tiene observación pendiente de guardar (generada o editada). Habilita solo "Guardar observaciones".</summary>
    private bool TieneObservacionesPendientesGuardar
    {
        get
        {
            if (_productoObservaciones == null) return false;
            var item = _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID);
            return item != null && item.ObservacionesPendienteGuardar;
        }
    }

    /// <summary>True si hay observación y/o descripciones pendientes de guardar (modificadas a mano o generadas con OpenAI). Usado para habilitar el botón verde.</summary>
    private bool TieneObservacionesODescripcionesPendientesGuardar
    {
        get
        {
            if (_productoObservaciones == null) return false;
            var item = _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID);
            return item != null && (item.ObservacionesPendienteGuardar || item.DescripcionCortaPendienteGuardar || item.DescripcionLargaPendienteGuardar);
        }
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
    /// Corrige solo secuencias RTF claramente mal formadas que rompen el RichTextBox del sistema viejo.
    /// NO intenta “mejorar” el castellano ni cambiar palabras: únicamente quita basura técnica (dobles barras,
    /// comillas extra después de \'xx, \u769?, \~n, etc.). Se aplica antes de normalizar (modal y modo lista).
    /// </summary>
    private static string RepairRtfEscapesFromModel(string rtf)
    {
        if (string.IsNullOrEmpty(rtf)) return rtf ?? "";
        var s = rtf;
        // 1) Doble escape: \\\' debe ser \' (si no, la API guarda d\'\'eda y el visor falla). Colapsar en bucle por si hay \\\\' etc.
        while (s.Contains("\\\\'", StringComparison.Ordinal))
            s = s.Replace("\\\\'", "\\'", StringComparison.Ordinal);
        // 2) Secuencia \'\' (apóstrofo duplicado) → \' (ej. d\'\'eda, presentaci\'\'f3n, peque\'\'f1os).
        while (s.Contains("\\'\\'", StringComparison.Ordinal))
            s = s.Replace("\\'\\'", "\\'", StringComparison.Ordinal);
        // 3) Comilla/apóstrofo extra después de \'XX (ej. decoraci\'f3'n). Esto rompe el parser RTF.
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"(\\'[0-9a-fA-F]{2})'",
            "$1",
            System.Text.RegularExpressions.RegexOptions.None);
        // 4) ñ mal guardada como \~n (ej. Dise~nado) → \'f1 (ñ en RTF).
        s = s.Replace("\\~n", "\\'f1", StringComparison.Ordinal);
        // 5) Unicode ú mal escrito: \'u00fa o \'u00fa, → \'fa (latin-1 ú). El patrón original no es RTF válido.
        s = s.Replace("\\'u00fa,", "\\'fa", StringComparison.Ordinal);
        s = s.Replace("\\'u00fa", "\\'fa", StringComparison.Ordinal);
        // 6) \d o \d1 incrustados en palabras (ej. m\'f3\delo). \d no es una secuencia RTF válida aquí y rompe el RichTextBox.
        s = s.Replace("\\d1", "d", StringComparison.Ordinal);
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"\\d([a-zA-Z])",
            "d$1",
            System.Text.RegularExpressions.RegexOptions.None);
        // 7) Secuencias \'vocal sin hex (\'a, \'e, \'i, \'o, \'u) que NO van seguidas de dos dígitos hex:
        //    el modelo a veces escribe \'o n, \'i a, etc. Eso no es RTF válido y rompe el RichTextBox.
        //    Las mapeamos a sus códigos Latin-1 correctos.
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"\\'a(?![0-9a-fA-F]{2})",
            "\\'e1",
            System.Text.RegularExpressions.RegexOptions.None);
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"\\'e(?![0-9a-fA-F]{2})",
            "\\'e9",
            System.Text.RegularExpressions.RegexOptions.None);
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"\\'i(?![0-9a-fA-F]{2})",
            "\\'ed",
            System.Text.RegularExpressions.RegexOptions.None);
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"\\'o(?![0-9a-fA-F]{2})",
            "\\'f3",
            System.Text.RegularExpressions.RegexOptions.None);
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"\\'u(?![0-9a-fA-F]{2})",
            "\\'fa",
            System.Text.RegularExpressions.RegexOptions.None);

        // Caso real visto: "\'e91reas" (áreas) → "\'e1reas".
        // El modelo mezcló \'e9 (é) con un "1" suelto. Convertimos al código correcto para á.
        s = s.Replace("\\'e91reas", "\\'e1reas", StringComparison.Ordinal);
        // Variante general: est\'e91 dise\'f1ado, contempor\'e91neo, tr\'e91nsito → est\'e1..., contempor\'e1neo, tr\'e1nsito.
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"\\'e91([A-Za-zñÑ])",
            "\\'e1$1",
            System.Text.RegularExpressions.RegexOptions.None);
        // Dígitos pegados inmediatamente después de una secuencia \'xx (ej. despu\'e99s → despu\'e9s).
        // El usuario no quiere números en observaciones, y en RTF correcto no debería haber dígitos justo detrás de \'xx.
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"(\\'[0-9a-fA-F]{2})\d+",
            "$1",
            System.Text.RegularExpressions.RegexOptions.None);

        // 8) Espacio ilegal entre la secuencia \'xx y la letra siguiente en medio de palabra:
        //    ejemplos: "decoraci\'f3 n", "clim\'e1 ticas". El espacio rompe la palabra y en vista previa se ve "decoració n".
        //    Para vocales y ñ, si hay un espacio seguido de letra, lo colapsamos.
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"(\\'(?:e1|e9|ed|f3|fa|f1|fc))\s+([A-Za-zñÑ])",
            "$1$2",
            System.Text.RegularExpressions.RegexOptions.None);

        // 9) Caso muy frecuente: el modelo escribe mal "Características" como "Caracter\'e9dsticas" (é + d...).
        //    Lo normalizamos a la forma correcta "Caracter\'edsticas" (í en RTF).
        s = s.Replace("Caracter\\'e9dsticas", "Caracter\\'edsticas", StringComparison.Ordinal);
        s = s.Replace("caracter\\'e9dsticas", "caracter\\'edsticas", StringComparison.Ordinal);

        // 8) Restos de entidades HTML "acute" pegadas a \'XX (ej. m\'f3acute;dulo, ergon\'f3acutemico).
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"(\\'[0-9a-fA-F]{2})acute;?",
            "$1",
            System.Text.RegularExpressions.RegexOptions.None);
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"(\\'[0-9a-fA-F]{2})acut([a-zA-Z])",
            "$1$2",
            System.Text.RegularExpressions.RegexOptions.None);
        // 9) Acento combinatorio Unicode \u769? (a veces devuelto por modelos). El RichTextBox del sistema viejo no lo entiende.
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"(\\'[0-9a-fA-F]{2})\\u769\?",
            "$1",
            System.Text.RegularExpressions.RegexOptions.None);
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"\\u769\?",
            "",
            System.Text.RegularExpressions.RegexOptions.None);
        return s;
    }

    /// <summary>
    /// Asegura que el texto devuelto por OpenAI/Gemini sea RTF válido para vista previa y para guardar en API (RichTextBox).
    /// Si la respuesta ya empieza con {\rtf1, se extrae y normaliza. Si viene en bloque de código (```), se quita el envoltorio.
    /// Si el modelo devolvió texto plano, se envuelve en un documento RTF mínimo para que RtfToHtmlConverter y el backend lo interpreten bien.
    /// </summary>
    private static string EnsureValidRtfFromOpenAI(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var rtf = raw.Trim();
        // Quitar bloque de código markdown (```rtf o ```)
        if (rtf.StartsWith("```", StringComparison.Ordinal))
        {
            var first = rtf.IndexOf('\n');
            var last = rtf.LastIndexOf("```", StringComparison.Ordinal);
            if (first >= 0 && last > first + 1)
                rtf = rtf.Substring(first + 1, last - first - 1).Trim();
        }
        // Extraer el bloque RTF si está embebido en explicación (ej. "Aquí está el RTF: {\rtf1 ... }")
        var rtfStart = rtf.IndexOf("{\\rtf", StringComparison.OrdinalIgnoreCase);
        if (rtfStart > 0)
        {
            var fromRtf = rtf.Substring(rtfStart);
            var lastBrace = fromRtf.LastIndexOf('}');
            if (lastBrace > 0)
                rtf = fromRtf.Substring(0, lastBrace + 1);
            else
                rtf = fromRtf;
        }
        else if (rtfStart == 0)
        {
            var lastBrace = rtf.LastIndexOf('}');
            if (lastBrace > 0)
                rtf = rtf.Substring(0, lastBrace + 1);
        }
        // Si ya es RTF válido (empieza con {\rtf), reparar escapes mal formados que a veces devuelve el modelo y normalizar
        if (rtf.StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase))
        {
            rtf = RepairRtfEscapesFromModel(rtf);
            rtf = NormalizeRtfForApi(rtf);
            // Usar exactamente el mismo formato que espera la API / RichTextBox (incluye quitar "d " inicial, \deff0, etc.)
            return EnsureRtfMatchesApiExample(rtf);
        }
        // Texto plano: envolver en RTF mínimo para que la vista previa y el RichTextBox lo muestren y almacenen correctamente
        var escaped = new StringBuilder();
        foreach (var c in rtf)
        {
            if (c == '\\') escaped.Append("\\\\");
            else if (c == '{') escaped.Append("\\{");
            else if (c == '}') escaped.Append("\\}");
            else if (c == '\r') { }
            else if (c == '\n') escaped.Append("\\par\n");
            else escaped.Append(c);
        }
        var minimalRtf = "{\\rtf1\\ansi\\deff0 {\\fonttbl {\\f0 Calibri;}}\\f0\\par\n" + escaped + "\\par }";
        minimalRtf = NormalizeRtfForApi(minimalRtf);
        return EnsureRtfMatchesApiExample(minimalRtf);
    }

    /// <summary>
    /// Deja el RTF en el formato exacto del ejemplo de la API: solo {\rtf1\ansi, \b, \b0, \par, \'xx.
    /// Quita \deff0 y {\fonttbl ...} que algunos visores RTF no reconocen y causan "Estructura no reconocida".
    /// </summary>
    private static string EnsureRtfMatchesApiExample(string rtf)
    {
        if (string.IsNullOrEmpty(rtf)) return rtf ?? "";
        var s = rtf;
        // Quitar bloque de fuente que no está en el ejemplo y puede romper el visor (InfoProducto RichTextBox)
        s = s.Replace("\\deff0 {\\fonttbl {\\f0 Calibri;}}\\f0\\par\n", "", StringComparison.Ordinal);
        s = s.Replace("\\deff0{\\fonttbl{\\f0 Calibri;}}\\f0\\par\n", "", StringComparison.Ordinal);
        // Formato del ejemplo BIEN: \ansi\b sin espacio entre ambos (en MAL aparece "\\ansi \\b")
        s = s.Replace("\\ansi \\b", "\\ansi\\b", StringComparison.Ordinal);
        // \ansi \n (espacio + salto) → \ansi\par\n como en el ejemplo BIEN
        s = s.Replace("\\ansi \n", "\\ansi\\par\n", StringComparison.Ordinal);
        // Salto de línea suelto (sin \par delante) → \par\n para consistencia con el ejemplo BIEN (no tocar el \n que ya va tras \par)
        s = System.Text.RegularExpressions.Regex.Replace(s, @"(?<!\\par)\n", "\\par\n", System.Text.RegularExpressions.RegexOptions.None);
        // Ejemplo BIEN no tiene espacio tras \par\n; MAL sí ("\\par\n Este"). Quitar espacio tras \par\n.
        while (s.Contains("\\par\n ", StringComparison.Ordinal))
            s = s.Replace("\\par\n ", "\\par\n", StringComparison.Ordinal);
        // Quitar espacio antes de \par (ej. " \\par" → "\par")
        while (s.Contains(" \\par", StringComparison.Ordinal))
            s = s.Replace(" \\par", "\\par", StringComparison.Ordinal);
        // "d " sobrante justo después de uno o más \par (ej. "\par\n\par\nd Este producto...").
        // El patrón "(\\par(\r?\n)) + d " se reemplaza por los mismos \par + saltos pero SIN la "d ".
        s = System.Text.RegularExpressions.Regex.Replace(
            s,
            @"(\\par(?:\r?\n))+d ",
            "$1",
            System.Text.RegularExpressions.RegexOptions.None);
        // Asegurar \par seguido de \n como en el ejemplo (evita problemas de parsing)
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\\par(?!\n)", "\\par\n", System.Text.RegularExpressions.RegexOptions.None);
        return s;
    }

    /// <summary>
    /// Convierte TEXTO PLANO (en español) devuelto por OpenAI en un RTF canónico para observaciones,
    /// con el mismo estilo que las observaciones creadas desde el RepositorioDRR:
    /// - Cabecera mínima {\rtf1\ansi
    /// - Párrafos con \par
    /// - Encabezados en negrita "Características:" y "Uso:".
    /// - Viñetas como líneas que empiezan con "- ".
    /// </summary>
    private static string PlainTextObservacionesToRtf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "{\\rtf1\\ansi\\par\n}";

        var sb = new StringBuilder();
        sb.Append("{\\rtf1\\ansi\\par\n");

        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        // Quitar CUALQUIER número del texto de observación (el modelo a veces mete "esté1", "3 opciones", etc.
        // y el usuario no quiere ver dígitos en las observaciones generadas).
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"\d+",
            "",
            System.Text.RegularExpressions.RegexOptions.None);
        var lines = normalized.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.Append("\\par\n");
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.Equals("Características:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Caracteristicas:", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\\b Caracter\\'edsticas:\\b0\\par\n");
                continue;
            }

            if (trimmed.Equals("Uso:", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\\b Uso:\\b0\\par\n");
                continue;
            }

            // Viñetas: línea que empieza con "- "
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                AppendPlainTextAsRtf(sb, trimmed);
                sb.Append("\\par\n");
                continue;
            }

            // Párrafo normal
            AppendPlainTextAsRtf(sb, trimmed);
            sb.Append("\\par\n");
        }

        sb.Append("}");
        // Pasar por el mismo pipeline de normalización / compatibilidad que el resto del sistema.
        var rtf = sb.ToString();
        rtf = RepairRtfEscapesFromModel(rtf);
        rtf = NormalizeRtfForApi(rtf);
        rtf = EnsureRtfMatchesApiExample(rtf);
        return rtf;
    }

    /// <summary>
    /// Escapa una línea de texto plano a RTF (backslash, llaves, saltos de línea y caracteres no ASCII -> \'xx o \uNNN?).
    /// Lógica equivalente a HtmlToRtfConverter.AppendCharToRtf pero aplicada a texto plano.
    /// </summary>
    private static void AppendPlainTextAsRtf(StringBuilder sb, string line)
    {
        foreach (var c in line)
        {
            if (c == '\\') { sb.Append("\\\\"); continue; }
            if (c == '{') { sb.Append("\\{"); continue; }
            if (c == '}') { sb.Append("\\}"); continue; }
            if (c == '\n' || c == '\r') { sb.Append("\\par "); continue; }

            if (c >= 0x20 && c <= 0x7E)
            {
                sb.Append(c);
                continue;
            }

            var code = (int)c;
            if (code >= 0x80 && code <= 0xFF)
            {
                sb.Append("\\'").Append(code.ToString("x2"));
                continue;
            }
            if (code > 0 && code <= 0xFFFF)
                sb.Append("\\u").Append(code).Append('?');
            else
                sb.Append('?');
        }
    }

    /// <summary>
    /// Normaliza el RTF para enviarlo a la API en el mismo formato que devuelve la API (ej. observacion de producto):
    /// {\rtf1\ansi, \b, \b0, \par, y caracteres acentuados como \'xx (dos hex en minúscula: \'e1 á, \'ed í, \'f3 ó, \'fa ú, \'f1 ñ).
    /// Solo ASCII en el payload; las secuencias \'XX se normalizan a minúsculas para coincidir con el almacenamiento correcto.
    /// </summary>
    private static string NormalizeRtfForApi(string? rtf)
    {
        if (string.IsNullOrEmpty(rtf)) return rtf ?? "";
        var sb = new StringBuilder(rtf.Length * 2);
        var i = 0;
        while (i < rtf.Length)
        {
            var c = rtf[i];
            var code = (int)c;

            // Secuencia RTF ya válida \'XX (dos hex) → normalizar a minúsculas para coincidir con el formato de la API (ej. \'f3, \'ed, \'fa)
            if (c == '\\' && i + 3 < rtf.Length && rtf[i + 1] == '\'' && IsHexRtf(rtf[i + 2]) && IsHexRtf(rtf[i + 3]))
            {
                sb.Append("\\'").Append(char.ToLowerInvariant(rtf[i + 2])).Append(char.ToLowerInvariant(rtf[i + 3]));
                i += 4;
                continue;
            }
            // Secuencia \uN? o \uNNN? (decimal) o \u-123? → no tocar
            if (c == '\\' && i + 2 < rtf.Length && rtf[i + 1] == 'u')
            {
                var start = i;
                i += 2;
                if (i < rtf.Length && rtf[i] == '-') i++;
                while (i < rtf.Length && char.IsDigit(rtf[i])) i++;
                if (i < rtf.Length && rtf[i] == '?') i++;
                sb.Append(rtf, start, i - start);
                continue;
            }

            if (code <= 0x7F)
            {
                sb.Append(c);
                i++;
            }
            else if (code >= 0x80 && code <= 0xFF)
            {
                sb.Append("\\'").Append(code.ToString("x2"));
                i++;
            }
            else
            {
                sb.Append("\\u").Append(code).Append('?');
                i++;
            }
        }
        return sb.ToString();
    }

    private static bool IsHexRtf(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

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
        // No forzar foco al overlay del modal Observaciones: si se hace en cada AfterRender, le quita el foco al textarea del prompt y el usuario no puede editarlo.
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
            {
                _observacionesRtf = HtmlToRtfConverter.ToRtf(html);
                // Marcar observación como pendiente de guardar en el ítem para habilitar botones de guardar.
                var item = _productoObservaciones != null ? _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID) : null;
                if (item != null) { item.Observaciones = _observacionesRtf; item.ObservacionesPendienteGuardar = true; }
            }
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
            var prompt = $@"Eres un asistente que escribe observaciones de productos para un catálogo.

Producto: {desc}. Código de barras: {barra}.

Información obtenida de búsqueda en internet:
{snippetsText}

Responde ÚNICAMENTE con texto plano en español. Usa acentos y ñ correctamente (ó, ñ, á, é, í, ú, ü). NO uses formato RTF ni secuencias \u o \', ni markdown. Escribe observaciones útiles (descripción, uso, características). Separa párrafos con saltos de línea. Si no hay información útil, un párrafo breve con la descripción del producto.";
            var result = await GeminiService.GenerateTextAsync(prompt, apiKeyGemini);
            if (_productoObservaciones == null) { _generandoObservaciones = false; await InvokeAsync(StateHasChanged); return; }
            if (result != null && result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                // Texto plano → RTF válido sin símbolos raros (nosotros generamos el RTF).
                _observacionesRtf = EnsureValidRtfFromOpenAI(result.Text);
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

    /// <summary>
    /// Abre la modal de opciones (abreviar corta, autocompletar larga, editar prompts) antes de generar con OpenAI.
    /// </summary>
    private void GenerarObservacionesConOpenAIAsync()
    {
        if (_productoObservaciones == null) return;
        var apiKey = _openAiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _errorObservaciones = "Ingresá la API key de OpenAI en «Configurar API keys» (modal al entrar si falta).";
            StateHasChanged();
            return;
        }
        var desc = _productoObservaciones.DescripcionLarga ?? "";
        var barra = _productoObservaciones.CodigoBarra ?? "";
        if (string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(barra))
        {
            _errorObservaciones = "El producto debe tener descripción o código de barras.";
            StateHasChanged();
            return;
        }
        _errorObservaciones = null;
        AbrirModalOpcionesGeneracion(esMasivo: false);
    }

    /// <summary>
    /// Construye el prompt por defecto para que OpenAI genere observaciones en TEXTO PLANO (no RTF),
    /// con buena ortografía en español. El nombre y el código de barras se usan solo como contexto,
    /// pero el modelo tiene prohibido devolverlos en el texto generado.
    /// </summary>
    private static string BuildPromptObservacionesRtfOpenAI(string descripcion, string codigoBarra)
    {
        return $@"Eres un asistente que escribe observaciones de productos para un catálogo. Responde ÚNICAMENTE con TEXTO PLANO en español (sin RTF, sin markdown, sin ```), listo para que otro sistema lo convierta a RTF.

Contexto (SOLO para que lo tengas en cuenta, NO para repetirlo en la respuesta):
- Nombre del producto: {descripcion}
- Código de barras: {codigoBarra}

Reglas de contenido (MUY IMPORTANTE):
- NO escribas el nombre/título específico del producto en ningún lugar del texto.
- NO escribas el código de barras ni números que parezcan un código de barras.
- No escribas listados de precios ni porcentajes.
- Solo escribe una descripción genérica del tipo de producto, sus características y su uso.

Formato del TEXTO PLANO que debes devolver:
1) No uses RTF ni markdown. Escribe texto normal con saltos de línea.
2) Usa exactamente la palabra ""Características:"" como encabezado de la sección de características, en una línea separada.
3) Usa exactamente la palabra ""Uso:"" como encabezado de la sección de uso, en una línea separada.
4) Debajo de ""Características:"" escribe viñetas con el formato ""- texto de la característica"" (un guion, un espacio y el texto).
5) Debajo de ""Uso:"" escribe uno o dos párrafos explicando el uso recomendado.

Estructura sugerida del TEXTO:
- Párrafo inicial describiendo de forma genérica para qué sirve el producto.
- Encabezado ""Características:"" en una línea.
- Viñetas con ""- "" para cada característica.
- Encabezado ""Uso:"" en una línea.
- Uno o dos párrafos explicando recomendaciones de uso en forma genérica.

Verificación antes de responder (MUY IMPORTANTE, HAZLO SIEMPRE):
- Lee mentalmente TODO el texto ANTES de devolverlo y aplícale una revisión ortográfica estricta en español.
- Asegúrate de que:
  - Todas las palabras estén completas y separadas por espacios (nunca pegues palabras como ""reproduccioncontinua"" o ""diseñop""; conviértelas en frases correctas como ""reproducción continua"" o ""diseño que permite..."").
  - No haya sílabas sueltas ni cortes raros (""mfavorita"", ""comp-to"", ""est3tico"", ""clá1sico"", etc.). Corrige cualquier palabra rota por su versión correcta en español.
  - Los acentos y la ñ estén correctamente escritos en texto normal: á, é, í, ó, ú, ñ.
  - Las oraciones tengan sentido, usen puntuación básica y no repitan constantemente las mismas frases.
  - No haya faltas de ortografía evidentes en español: corrige palabras mal escritas como ""Caracteristicas"", ""caracteristcias"", ""deoración"", ""estetico"", ""clasico"", etc., y devuelve siempre la forma correcta ""Características"", ""características"", ""decoración"", ""estético"", ""clásico"", etc.
- Si detectas una palabra rara, sin sentido, con números mezclados o pegada, corrígela por una forma clara y natural en español ANTES de devolver la respuesta.

Devuelve solo el TEXTO PLANO cuando estés seguro de que la respuesta es correcta, natural en español, SIN errores ortográficos y completa.

En ningún momento escribas el nombre real del producto ni el código de barras.";
    }

    /// <summary>Construye un único prompt para que OpenAI devuelva observación + opcional descripción corta + opcional descripción larga, con delimitadores.</summary>
    private string BuildPromptObservacionesYDescripcionesOpenAI(string descripcion, string codigoBarra, bool incluirCorta, bool incluirLarga)
    {
        var sb = new StringBuilder();
        sb.Append(BuildPromptObservacionesRtfOpenAI(descripcion, codigoBarra));
        sb.Append("\n\n---\n\nIMPORTANTE - Formato de respuesta:\n");
        sb.Append("1) Responde PRIMERO con el texto de la OBSERVACIÓN en texto plano (la misma estructura: Características:, Uso:, viñetas). No uses delimitadores antes ni después de la observación.\n");
        if (incluirCorta)
        {
            sb.Append("2) Después de la observación, escribe exactamente en una línea: ---DESC_CORTA---\n");
            sb.Append("3) En la siguiente línea escribe ÚNICAMENTE la descripción corta abreviada (máx. 30 caracteres).\n");
            sb.Append("Instrucciones para la descripción corta: ");
            sb.Append(_promptAbreviarCorta?.Trim() ?? GetPromptAbreviarCortaDefault());
            sb.Append("\n");
        }
        if (incluirLarga)
        {
            sb.Append(incluirCorta ? "4" : "2");
            sb.Append(") Luego escribe exactamente en una línea: ---DESC_LARGA---\n");
            sb.Append(incluirCorta ? "5" : "3");
            sb.Append(") En la(s) siguiente(s) línea(s) escribe ÚNICAMENTE la descripción larga.\n");
            sb.Append("Instrucciones para la descripción larga: ");
            sb.Append(_promptDescripcionLarga?.Trim() ?? GetPromptDescripcionLargaDefault());
            sb.Append("\n");
        }
        sb.Append("\nResponde solo con el contenido solicitado, usando los delimitadores exactos cuando corresponda.");
        return sb.ToString();
    }

    /// <summary>Parsea la respuesta de OpenAI que puede contener observación y opcionalmente ---DESC_CORTA--- y ---DESC_LARGA---.</summary>
    private static void ParseObservacionesYDescripcionesResponse(string raw, bool pedirCorta, bool pedirLarga,
        out string observacionPlano, out string? descripcionCorta, out string? descripcionLarga)
    {
        observacionPlano = "";
        descripcionCorta = null;
        descripcionLarga = null;
        if (string.IsNullOrWhiteSpace(raw)) return;

        const string delimCorta = "---DESC_CORTA---";
        const string delimLarga = "---DESC_LARGA---";
        var text = raw.Trim();

        if (pedirCorta && text.Contains(delimCorta, StringComparison.OrdinalIgnoreCase))
        {
            var idx = text.IndexOf(delimCorta, StringComparison.OrdinalIgnoreCase);
            observacionPlano = text.Substring(0, idx).Trim();
            var afterCorta = text.Substring(idx + delimCorta.Length).Trim();
            if (pedirLarga && afterCorta.Contains(delimLarga, StringComparison.OrdinalIgnoreCase))
            {
                var idxL = afterCorta.IndexOf(delimLarga, StringComparison.OrdinalIgnoreCase);
                descripcionCorta = afterCorta.Substring(0, idxL).Trim();
                descripcionLarga = afterCorta.Substring(idxL + delimLarga.Length).Trim();
            }
            else
            {
                descripcionCorta = afterCorta;
            }
        }
        else if (pedirLarga && text.Contains(delimLarga, StringComparison.OrdinalIgnoreCase))
        {
            var idx = text.IndexOf(delimLarga, StringComparison.OrdinalIgnoreCase);
            observacionPlano = text.Substring(0, idx).Trim();
            descripcionLarga = text.Substring(idx + delimLarga.Length).Trim();
        }
        else
        {
            observacionPlano = text;
        }
    }

    /// <summary>Se ejecuta al hacer clic en "Generar" en la modal de opciones. Un producto: actualiza modal; masivo: recorre selección.</summary>
    private async Task EjecutarGeneracionOpenAIConfirmarAsync()
    {
        var apiKey = _openAiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _errorObservaciones = "Falta la API key de OpenAI.";
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_opcionesGeneracionEsMasivo)
        {
            CerrarModalOpcionesGeneracion();
            await GenerarObservacionesMasivoOpenAIConLoopAsync();
            return;
        }

        // Un solo producto
        if (_productoObservaciones == null) { CerrarModalOpcionesGeneracion(); return; }
        var desc = _productoObservaciones.DescripcionLarga ?? "";
        var barra = _productoObservaciones.CodigoBarra ?? "";
        if (string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(barra))
        {
            _errorObservaciones = "El producto debe tener descripción o código de barras.";
            CerrarModalOpcionesGeneracion();
            await InvokeAsync(StateHasChanged);
            return;
        }

        _generandoObservaciones = true;
        CerrarModalOpcionesGeneracion();
        await InvokeAsync(StateHasChanged);

        try
        {
            var basePrompt = BuildPromptObservacionesYDescripcionesOpenAI(desc, barra, _opcionesAbreviarCorta, _opcionesAutocompletarLarga);
            var extra = _observacionesPromptExtra?.Trim();
            var prompt = string.IsNullOrWhiteSpace(extra)
                ? basePrompt
                : $"{basePrompt}\n\n---\n\nInstrucciones adicionales del usuario:\n{extra}";
            var result = await OpenAITextService.GenerateTextAsync(prompt, apiKey);
            if (_productoObservaciones == null) { _generandoObservaciones = false; await InvokeAsync(StateHasChanged); return; }
            if (result != null && result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                ParseObservacionesYDescripcionesResponse(result.Text, _opcionesAbreviarCorta, _opcionesAutocompletarLarga,
                    out var observacionPlano, out var corta, out var larga);
                _observacionesRtf = PlainTextObservacionesToRtf(observacionPlano);
                _descripcionCortaGenerada = corta;
                _descripcionLargaGenerada = larga;
                // Dejar en memoria en el ítem para que al cerrar y reabrir la modal se vea lo generado y quede pendiente de guardar.
                if (_productoObservaciones != null)
                {
                    var it = _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID);
                    if (it != null)
                    {
                        it.Observaciones = _observacionesRtf;
                        it.ObservacionesPendienteGuardar = true;
                        if (corta != null) { it.DescripcionCorta = corta; it.DescripcionCortaPendienteGuardar = true; }
                        if (larga != null) { it.DescripcionLarga = larga; it.DescripcionLargaPendienteGuardar = true; }
                    }
                }
                _observacionesPreviewNeedsSyncFromRtf = true;
                _observacionesSkipNextSyncFromPreview = true;
            }
            else
                _errorObservaciones = result?.ErrorMessage ?? "OpenAI no devolvió texto.";
        }
        catch (Exception ex)
        {
            _errorObservaciones = ex.Message;
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

        // Reparar, normalizar y dejar el RTF en el formato exacto del ejemplo de la API (sin \deff0/\fonttbl, \par\n).
        var rtfAntes = _observacionesRtf;
        var texto = string.IsNullOrWhiteSpace(rtfAntes) ? null : EnsureRtfMatchesApiExample(NormalizeRtfForApi(RepairRtfEscapesFromModel(rtfAntes)));

        try
        {
            var nonAsciiEnNormalizado = texto == null ? 0 : texto.Count(c => (int)c > 0x7F);
            await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones DIAGNÓSTICO", JsonSerializer.Serialize(new
            {
                codigoID = _productoObservaciones!.ProductoID,
                rtfLongitudAntes = rtfAntes?.Length ?? 0,
                observacionLongitudDespues = texto?.Length ?? 0,
                noAsciiEnEnvio = nonAsciiEnNormalizado,
                previewAntes = (rtfAntes?.Length ?? 0) > 0 ? (rtfAntes!.Length > 180 ? rtfAntes.Substring(0, 180) + "…" : rtfAntes) : "(vacío)",
                previewEnvio = (texto?.Length ?? 0) > 0 ? (texto!.Length > 180 ? texto.Substring(0, 180) + "…" : texto) : "(null)"
            }));
        }
        catch { /* no romper guardado si falla el log */ }

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
            var swPatch = Stopwatch.StartNew();
            var result = await ProductoPatch.PatchProductoAsync(request, _token);
            swPatch.Stop();
            if (!result.Success)
            {
                _errorObservaciones = "No se pudo guardar las observaciones en la API.";
                await MostrarToastAsync("No se pudo guardar las observaciones en la API.", true);
                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones ERROR", JsonSerializer.Serialize(new
                    {
                        tiempoMs = swPatch.ElapsedMilliseconds,
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
                    tiempoMs = swPatch.ElapsedMilliseconds,
                    tiempoSeg = Math.Round(swPatch.Elapsed.TotalSeconds, 2),
                    codigoID = request.CodigoID,
                    observacionLongitudEnviada = request.Observacion?.Length ?? 0,
                    statusCode = result.StatusCode,
                    responseBody = (result.ResponseBody?.Length ?? 0) > 500 ? result.ResponseBody.Substring(0, 500) + "…" : result.ResponseBody
                }));
            }
            catch { }
            // No cerrar la modal: el usuario puede seguir editando descripciones o guardar todo junto.
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

    /// <summary>
    /// Guarda solo descripción corta y/o larga.
    /// IMPORTANTE: el PATCH de descripciones aún NO está disponible en la API,
    /// por lo que por ahora solo actualizamos el estado en memoria y mostramos un aviso.
    /// Cuando el backend soporte descripcionCorta/descripcionLarga, se puede reactivar el PATCH comentado más abajo.
    /// </summary>
    private async Task GuardarDescripcionesEnProductoAsync()
    {
        if (_productoObservaciones == null || string.IsNullOrWhiteSpace(_token)) return;
        var tieneCorta = !string.IsNullOrWhiteSpace(_descripcionCortaGenerada);
        var tieneLarga = !string.IsNullOrWhiteSpace(_descripcionLargaGenerada);
        if (!tieneCorta && !tieneLarga) return;

        var item = _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID);
        if (item != null)
        {
            if (tieneCorta) { item.DescripcionCorta = _descripcionCortaGenerada; item.DescripcionCortaPendienteGuardar = false; }
            if (tieneLarga) { item.DescripcionLarga = _descripcionLargaGenerada; item.DescripcionLargaPendienteGuardar = false; }
        }

        // Por ahora NO llamamos a PATCH para descripciones porque el contrato de la API todavía no las soporta.
        await MostrarToastAsync("Descripciones actualizadas en memoria.", false);
    }

    /// <summary>
    /// Guarda observación (PATCH a la API) y descripciones (en memoria; PATCH aún no disponible).
    /// No cierra la modal. Funciona igual en grid y en lista.
    /// </summary>
    private async Task GuardarObservacionesYDescripcionesEnProductoAsync()
    {
        if (_productoObservaciones == null || string.IsNullOrWhiteSpace(_token)) return;

        var item = _items.FirstOrDefault(x => x.ProductoID == _productoObservaciones.ProductoID);
        var guardarObservacion = !string.IsNullOrWhiteSpace(_observacionesRtf);
        var guardarDescripciones = !string.IsNullOrWhiteSpace(_descripcionCortaGenerada) || !string.IsNullOrWhiteSpace(_descripcionLargaGenerada);

        if (!guardarObservacion && !guardarDescripciones) return;

        _guardandoObservaciones = true;
        _errorObservaciones = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            if (guardarObservacion)
            {
                var rtfAntes = _observacionesRtf;
                var texto = EnsureRtfMatchesApiExample(NormalizeRtfForApi(RepairRtfEscapesFromModel(rtfAntes ?? "")));
                if (item != null)
                {
                    item.Observaciones = texto;
                    item.ObservacionesPendienteGuardar = false;
                }
                var request = new ProductoPatchRequest
                {
                    CodigoID = _productoObservaciones.ProductoID,
                    ImagenEspecified = false,
                    Imagen = "null",
                    ObservacionEspecified = true,
                    Observacion = texto
                };
                var result = await ProductoPatch.PatchProductoAsync(request, _token);
                if (!result.Success)
                {
                    _errorObservaciones = "No se pudo guardar las observaciones en la API.";
                    await MostrarToastAsync(_errorObservaciones, true);
                    return;
                }
                await MostrarToastAsync("Observaciones guardadas correctamente.", false);
            }

            if (guardarDescripciones && item != null)
            {
                if (!string.IsNullOrWhiteSpace(_descripcionCortaGenerada))
                {
                    item.DescripcionCorta = _descripcionCortaGenerada;
                    item.DescripcionCortaPendienteGuardar = false;
                }
                if (!string.IsNullOrWhiteSpace(_descripcionLargaGenerada))
                {
                    item.DescripcionLarga = _descripcionLargaGenerada;
                    item.DescripcionLargaPendienteGuardar = false;
                }
                await MostrarToastAsync(guardarObservacion ? "Observaciones y descripciones actualizadas." : "Descripciones actualizadas en memoria.", false);
            }
        }
        catch (Exception ex)
        {
            _errorObservaciones = ex.Message;
            await MostrarToastAsync("Error: " + ex.Message, true);
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
        // Query completa para la API DRR: siempre código de barra + descripción completa (no se acorta).
        var queryInicial = ImageSearchQueryHelper.ConstruirQuery(barra, desc);

        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "MODAL_QUERY_INICIAL",
                JsonSerializer.Serialize(new
                {
                    productoID = p.ProductoID,
                    codigoBarra = string.IsNullOrWhiteSpace(barra) ? "(vacío)" : barra,
                    descripcionLongitud = desc.Length,
                    queryEnviadaALaApi = queryInicial,
                    mensaje = "Al abrir el modal se envía a la API código de barra + descripción completa (sin acortar)."
                }));
        }
        catch { /* logging no crítico */ }

        _error = null;
        // Textbox muestra y envía a la API la query completa (barra + desc); el usuario puede editarla y "Buscar de nuevo" enviará lo que escriba.
        var textoBusquedaInicial = queryInicial;
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
            // Primera llamada: código de barra + descripción completa. Si la API lanza (ej. status:"error") o devuelve vacío, reintentar una vez (la API DRR a veces falla a la primera).
            IReadOnlyList<string>? urls = null;
            if (!string.IsNullOrWhiteSpace(queryInicial))
            {
                try
                {
                    urls = await IntegrationImageSearch.SearchImageUrlsAsync(queryInicial, _token!);
                }
                catch
                {
                    urls = null;
                }
                if ((urls == null || urls.Count == 0) && _busquedaWeb != null)
                {
                    await Task.Delay(600);
                    try
                    {
                        if (_busquedaWeb != null)
                            urls = await IntegrationImageSearch.SearchImageUrlsAsync(queryInicial, _token!);
                    }
                    catch
                    {
                        urls ??= Array.Empty<string>();
                    }
                }
            }
            if ((urls == null || urls.Count == 0) && _busquedaWeb != null)
            {
                try
                {
                    urls = await BuscarUrlsIntegracionPorProductoAsync(barra, desc);
                }
                catch
                {
                    urls = null;
                }
                if ((urls == null || urls.Count == 0) && _busquedaWeb != null)
                {
                    await Task.Delay(600);
                    try
                    {
                        if (_busquedaWeb != null)
                            urls = await BuscarUrlsIntegracionPorProductoAsync(barra, desc);
                    }
                    catch
                    {
                        urls ??= Array.Empty<string>();
                    }
                }
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

        // Primera llamada: siempre según regla (barra+desc o solo desc). Si lanza (ej. status:"error") o vacío, reintentar una vez.
        IReadOnlyList<string>? urls = null;
        try
        {
            urls = await IntegrationImageSearch.SearchImageUrlsAsync(query, _token!);
            if (urls != null && urls.Count > 0)
                return urls;
        }
        catch
        {
            urls = null;
        }
        await Task.Delay(600);
        try
        {
            urls = await IntegrationImageSearch.SearchImageUrlsAsync(query, _token!);
            if (urls != null && urls.Count > 0)
                return urls;
        }
        catch
        {
            // Seguimos al fallback solo descripción.
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
                urls = await IntegrationImageSearch.SearchImageUrlsAsync(desc.Trim(), _token!);
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
            IReadOnlyList<string>? urls = null;
            try
            {
                urls = await IntegrationImageSearch.SearchImageUrlsAsync(q, _token!);
                if (urls != null && urls.Count > 0)
                    return urls;
            }
            catch
            {
                urls = null;
            }
            await Task.Delay(600);
            try
            {
                urls = await IntegrationImageSearch.SearchImageUrlsAsync(q, _token!);
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
        _busquedaWebCancelacionRazon = "Se cerró el modal de búsqueda mientras se descargaba la imagen.";
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

        _busquedaWeb.Descargando = true;
        _busquedaWeb.Error = null;
        StateHasChanged();

        _busquedaWebCancelacionRazon = "Se inició otra descarga (se eligió otra imagen o se volvió a guardar).";
        _busquedaWebCts?.Cancel();
        _busquedaWebCts?.Dispose();
        _busquedaWebCts = new CancellationTokenSource();
        _busquedaWebCancelacionRazon = null;
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
            bool fueNuestraCancelacion = _busquedaWebCts?.Token.IsCancellationRequested == true;
            bool tuvoRazonExplicita = !string.IsNullOrWhiteSpace(_busquedaWebCancelacionRazon);
            var razon = _busquedaWebCancelacionRazon
                ?? (fueNuestraCancelacion
                    ? "Motivo no registrado (posible cierre del modal o pestaña en segundo plano)."
                    : "La descarga fue interrumpida por la red o el servidor (timeout, conexión cerrada o imagen no accesible). No fue una cancelación manual.");
            var errorUsuario = (fueNuestraCancelacion || tuvoRazonExplicita)
                ? "Descarga cancelada. Elegí otra imagen."
                : "La descarga se interrumpió (timeout o servidor). Probá con otra imagen.";
            var logDetail = System.Text.Json.JsonSerializer.Serialize(new
            {
                evento = "Descarga cancelada",
                razon,
                productId,
                imageUrl = imageUrl?.Length > 200 ? imageUrl.Substring(0, 200) + "…" : imageUrl,
                timestamp = DateTime.UtcNow.ToString("o"),
                mensaje = "Ver el campo 'razon' para el detalle."
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _busquedaWebCancelacionRazon = null;
            if (_busquedaWeb != null)
            {
                _busquedaWeb.Error = errorUsuario;
                _busquedaWeb.ErrorLogDetail = logDetail;
            }
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "DESCARGA_CANCELADA", logDetail);
            }
            catch { }
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
        _busquedaWebCancelacionRazon = "El usuario hizo clic en «Cancelar» durante la descarga.";
        _busquedaWebCts?.Cancel();
    }

    /// <summary>Un clic en una miniatura: solo la selecciona (para luego guardar). No abre la modal de vista en grande.</summary>
    private void SeleccionarPreviewImagenSerpApi(string imageUrl)
    {
        if (_busquedaWeb == null) return;
        _busquedaWeb.SelectedImageUrl = imageUrl;
        _busquedaWeb.Error = null;
        _busquedaWeb.ErrorLogDetail = null;
        StateHasChanged();
    }

    /// <summary>Doble clic en una miniatura: abre la modal de vista en grande para ver la imagen.</summary>
    private void AbrirPreviewImagenBusquedaWeb(string imageUrl)
    {
        if (_busquedaWeb == null) return;
        _busquedaWeb.SelectedImageUrl = imageUrl;
        _busquedaWeb.Error = null;
        _busquedaWeb.ErrorLogDetail = null;
        _busquedaWebPreviewUrl = imageUrl;
        StateHasChanged();
    }

    /// <summary>Descarga la imagen seleccionada en el modal SerpAPI y la asigna al producto en memoria (ImagenPendienteGuardar = true). No llama a la API.</summary>
    private async Task GuardarImagenSeleccionadaSerpApiAsync()
    {
        if (_busquedaWeb == null || string.IsNullOrWhiteSpace(_busquedaWeb.SelectedImageUrl) || _busquedaWeb.ProductoID == 0) return;
        if (_busquedaWeb.Descargando) return;

        var imageUrl = _busquedaWeb.SelectedImageUrl;
        var productId = _busquedaWeb.ProductoID;

        // Marcar como descargando de inmediato para evitar doble clic (sobre todo en modo lista).
        _busquedaWeb.Descargando = true;
        _busquedaWeb.Error = null;
        StateHasChanged();

        _busquedaWebCancelacionRazon = "Se inició otra descarga (por ejemplo, doble clic en Guardar).";
        _busquedaWebCts?.Cancel();
        _busquedaWebCts?.Dispose();
        _busquedaWebCts = new CancellationTokenSource();
        _busquedaWebCancelacionRazon = null;
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
            bool fueNuestraCancelacion = _busquedaWebCts?.Token.IsCancellationRequested == true;
            bool tuvoRazonExplicita = !string.IsNullOrWhiteSpace(_busquedaWebCancelacionRazon);
            var razon = _busquedaWebCancelacionRazon
                ?? (fueNuestraCancelacion
                    ? "Motivo no registrado (posible cierre del modal o pestaña en segundo plano)."
                    : "La descarga fue interrumpida por la red o el servidor (timeout, conexión cerrada o imagen no accesible). No fue una cancelación manual.");
            var errorUsuario = (fueNuestraCancelacion || tuvoRazonExplicita)
                ? "Descarga cancelada."
                : "La descarga se interrumpió (timeout o servidor). Probá con otra imagen.";
            var logDetail = System.Text.Json.JsonSerializer.Serialize(new
            {
                evento = "Descarga cancelada",
                razon,
                productId,
                imageUrl = imageUrl?.Length > 200 ? imageUrl.Substring(0, 200) + "…" : imageUrl,
                timestamp = DateTime.UtcNow.ToString("o"),
                mensaje = "Ver el campo 'razon' para el detalle."
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            _busquedaWebCancelacionRazon = null;
            if (_busquedaWeb != null)
            {
                _busquedaWeb.Error = errorUsuario;
                _busquedaWeb.ErrorLogDetail = logDetail;
            }
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "DESCARGA_CANCELADA", logDetail);
            }
            catch { }
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

    /// <summary>Copia el log de detalle (ej. descarga cancelada) al portapapeles y muestra toast.</summary>
    private async Task CopiarLogBusquedaWebAsync()
    {
        if (_busquedaWeb?.ErrorLogDetail == null) return;
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", _busquedaWeb.ErrorLogDetail);
            await MostrarToastAsync("LOG copiado al portapapeles.", false);
        }
        catch
        {
            await MostrarToastAsync("No se pudo copiar al portapapeles.", true);
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
        // Por ahora solo enviamos PATCH de observaciones; las descripciones corta/larga no están soportadas en la API.
        var pendientesPatch = _items.Where(p => p.ObservacionesPendienteGuardar).ToList();
        if (pendientesImagen.Count == 0 && pendientesPatch.Count == 0)
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
            for (int i = 0; i < pendientesPatch.Count; i++)
            {
                var item = pendientesPatch[i];
                var observacionNormalizada = EnsureRtfMatchesApiExample(NormalizeRtfForApi(RepairRtfEscapesFromModel(item.Observaciones ?? "")));
                var request = new ProductoPatchRequest
                {
                    CodigoID = item.ProductoID,
                    ImagenEspecified = false,
                    Imagen = "null",
                    ObservacionEspecified = true,
                    Observacion = observacionNormalizada
                };
                try
                {
                    var noAscii = (observacionNormalizada?.Count(c => (int)c > 0x7F) ?? 0);
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones MASIVO ANTES", JsonSerializer.Serialize(new
                    {
                        indice = i + 1,
                        total = pendientesPatch.Count,
                        codigoID = request.CodigoID,
                        observacionLongitud = observacionNormalizada?.Length ?? 0,
                        noAsciiEnEnvio = noAscii,
                        preview = (observacionNormalizada?.Length ?? 0) > 120 ? observacionNormalizada!.Substring(0, 120) + "…" : observacionNormalizada
                    }));
                }
                catch { }

                var swPatch = Stopwatch.StartNew();
                var result = await ProductoPatch.PatchProductoAsync(request, _token);
                swPatch.Stop();
                if (result.Success)
                {
                    // Solo marcamos observaciones como guardadas; descripciones se guardan solo en memoria.
                    if (item.ObservacionesPendienteGuardar) item.ObservacionesPendienteGuardar = false;
                    okObs++;
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "PATCH Observaciones MASIVO OK", JsonSerializer.Serialize(new
                        {
                            indice = i + 1,
                            total = pendientesPatch.Count,
                            tiempoMs = swPatch.ElapsedMilliseconds,
                            codigoID = request.CodigoID,
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
                            total = pendientesPatch.Count,
                            tiempoMs = swPatch.ElapsedMilliseconds,
                            codigoID = request.CodigoID,
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
    /// Actualmente solo se intenta la primera URL devuelta por la API para respetar el orden de resultados,
    /// pero se deja este valor para posibles ajustes futuros.
    /// </summary>
    private const int MaxUrlsToTryPerProductoMasivo = 20;

    /// <summary>
    /// Intenta descargar una imagen desde la lista de URLs (búsqueda masiva).
    /// Se prueba la primera URL; si falla (timeout, cancelación, red), se reintenta una vez tras 600 ms.
    /// Si sigue fallando, se prueban las siguientes URLs (hasta 5) con un intento y un reintento cada una,
    /// para asignar imagen automáticamente sin que el usuario tenga que abrir la modal y guardar a mano.
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
        if (urls == null || urls.Count == 0)
            return (null, -1);

        const int maxUrlsToTry = 5;
        int urlsToTry = Math.Min(maxUrlsToTry, urls.Count);

        for (int urlIdx = 0; urlIdx < urlsToTry; urlIdx++)
        {
            var imageUrl = urls[urlIdx];
            if (string.IsNullOrWhiteSpace(imageUrl))
                continue;

            int urlIndex1Based = urlIdx + 1;
            try
            {
                await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration INTENTO_URL",
                    JsonSerializer.Serialize(new { indice, total, productId, codigo, urlIndex = urlIndex1Based, urlsDisponibles = urls.Count }));
            }
            catch { }

            // Primer intento de descarga para esta URL.
            string? dataUrl = await DescargarUnaImagenUrlAsync(imageUrl, productId, codigo, indice, total, urlIndex1Based, intento: 1);
            if (!string.IsNullOrWhiteSpace(dataUrl) && dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration FETCH_OK",
                        JsonSerializer.Serialize(new { indice, total, productId, codigo, urlIndex = urlIndex1Based }));
                }
                catch { }
                return (dataUrl, urlIndex1Based);
            }

            // Reintento tras 600 ms (mismo comportamiento que en la modal: si falla por timeout/cancelación, suele funcionar al reintentar).
            await Task.Delay(600);
            dataUrl = await DescargarUnaImagenUrlAsync(imageUrl, productId, codigo, indice, total, urlIndex1Based, intento: 2);
            if (!string.IsNullOrWhiteSpace(dataUrl) && dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration FETCH_OK_REINTENTO",
                        JsonSerializer.Serialize(new { indice, total, productId, codigo, urlIndex = urlIndex1Based, mensaje = "Éxito en el segundo intento." }));
                }
                catch { }
                return (dataUrl, urlIndex1Based);
            }
        }

        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration FALLA_TODAS_URLS",
                JsonSerializer.Serialize(new { indice, total, productId, codigo, urlsProbadas = urlsToTry, mensaje = "No se pudo descargar ninguna URL tras intentos y reintentos." }));
        }
        catch { }

        return (null, -1);
    }

    /// <summary>
    /// Descarga una sola URL a data URL. Usado por búsqueda masiva para intentar/reintentar cada imagen.
    /// Devuelve null si falla o el resultado no es una imagen válida.
    /// </summary>
    private async Task<string?> DescargarUnaImagenUrlAsync(
        string imageUrl,
        int productId,
        string? codigo,
        int indice,
        int total,
        int urlIndex1Based,
        int intento)
    {
        try
        {
            var dataUrl = await GoogleImageSearch.FetchImageAsDataUrlAsync(imageUrl);
            return dataUrl;
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
                        urlIndex = urlIndex1Based,
                        intento,
                        error = exFetch.Message,
                        imageUrlPreview = imageUrl.Length > 100 ? imageUrl.Substring(0, 100) + "…" : imageUrl
                    }));
            }
            catch { }
            return null;
        }
    }

    /// <summary>
    /// Asignación masiva de imágenes: para cada producto seleccionado busca en la API DRR (código de barra + descripción completa).
    /// Para cada producto obtiene URLs, descarga la primera imagen válida (con reintento automático si falla por timeout/red)
    /// y la asigna en memoria (ImagenPendienteGuardar). El usuario confirma con «Guardar cambios».
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
                // Query para API DRR: siempre código de barra + descripción completa (no se acorta).
                var queryPrimera = ImageSearchQueryHelper.ConstruirQuery(barra, desc);
                if (string.IsNullOrWhiteSpace(queryPrimera))
                {
                    idsSinImagen.Add(new { productId = p.ProductoID, codigo = p.Codigo, motivo = "descripción vacía" });
                    _bulkSearchingCurrent = i + 1;
                    await InvokeAsync(StateHasChanged);
                    continue;
                }

                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration QUERY_POR_PRODUCTO",
                        JsonSerializer.Serialize(new { productId = p.ProductoID, codigo = p.Codigo, barra, descCorta = desc.Length > 50 ? desc.Substring(0, 50) + "…" : desc, queryEnviadaALaApi = queryPrimera, mensaje = "Código de barra + descripción completa (sin acortar)." }));
                }
                catch { }

                try
                {
                    await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration PROCESANDO",
                        JsonSerializer.Serialize(new { indice = i + 1, total, productId = p.ProductoID, codigo = p.Codigo, tieneBarra = !string.IsNullOrWhiteSpace(barra), descCorta = desc.Length > 40 ? desc.Substring(0, 40) + "…" : desc }));
                }
                catch { }

                string? dataUrlAsignada = null;
                int urlIndexUsada = -1;
                IReadOnlyList<string>? urlsFinales = null;

                IReadOnlyList<string>? urls;
                try
                {
                    urls = await BuscarUrlsIntegracionPorProductoAsync(barra, desc);
                }
                catch (Exception exApi)
                {
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration ERROR_API",
                            JsonSerializer.Serialize(new { indice = i + 1, total, p.ProductoID, p.Codigo, descCorta = desc.Length > 40 ? desc.Substring(0, 40) + "…" : desc, error = exApi.Message }));
                    }
                    catch { }
                    idsSinImagen.Add(new { productId = p.ProductoID, codigo = p.Codigo, motivo = exApi.Message });
                    _bulkSearchingCurrent = i + 1;
                    await InvokeAsync(StateHasChanged);
                    continue;
                }

                if (urls != null && urls.Count > 0)
                {
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration URLs_OBTENIDAS",
                            JsonSerializer.Serialize(new { indice = i + 1, total, p.ProductoID, p.Codigo, urlsCount = urls.Count, primeraUrl = urls[0] }));
                    }
                    catch { }

                    _urlsBusquedaCachePorProducto[p.ProductoID] = new List<string>(urls);
                    var (dataUrl, urlIndex) = await IntentarDescargarAlgunaImagenDeUrlsAsync(p.ProductoID, p.Codigo, urls, i + 1, total);
                    if (!string.IsNullOrWhiteSpace(dataUrl))
                    {
                        dataUrlAsignada = dataUrl;
                        urlIndexUsada = urlIndex;
                        urlsFinales = urls;
                    }
                }
                else
                {
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration SIN_URLS",
                            JsonSerializer.Serialize(new { indice = i + 1, total, p.ProductoID, p.Codigo, queryEnviada = queryPrimera }));
                    }
                    catch { }
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
                            JsonSerializer.Serialize(new { indice = i + 1, total, productId = p.ProductoID, codigo = p.Codigo, urlIndexUsada, urlsDisponibles = urlsFinales.Count, mensaje = $"Imagen asignada (URL #{urlIndexUsada} de {urlsFinales.Count})." }));
                    }
                    catch { }
                }
                else
                {
                    var motivoFalla = (urls != null && urls.Count > 0)
                        ? "No se pudo descargar ninguna imagen (tras reintentos automáticos)."
                        : "No se encontraron imágenes con código de barra + descripción completa, ni al reintentar solo con descripción.";
                    idsSinImagen.Add(new { productId = p.ProductoID, codigo = p.Codigo, motivo = motivoFalla });
                    try
                    {
                        await JS.InvokeVoidAsync("__logAsignarImagenes", "BuscarImagenMasivoIntegration FALLA_ITEM",
                            JsonSerializer.Serialize(new { indice = i + 1, total, p.ProductoID, p.Codigo, motivo = motivoFalla }));
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
                var prompt = $@"Eres un asistente que escribe observaciones de productos para un catálogo.

Producto: {desc}. Código de barras: {barra}.

Información obtenida de búsqueda en internet:
{snippetsText}

Responde ÚNICAMENTE con texto plano en español. Usa acentos y ñ correctamente (ó, ñ, á, é, í, ú, ü). NO uses formato RTF ni secuencias \u o \', ni markdown. Escribe observaciones útiles (descripción, uso, características). Separa párrafos con saltos de línea. Si no hay información útil, un párrafo breve con la descripción del producto.";
                var result = await GeminiService.GenerateTextAsync(prompt, apiKeyGemini!);
                if (result != null && result.Success && !string.IsNullOrWhiteSpace(result.Text))
                {
                    // Texto plano → RTF canónico (mismo formato que el escritorio).
                    var rtf = PlainTextObservacionesToRtf(result.Text);
                    p.Observaciones = rtf;
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

    /// <summary>Abre la modal de opciones; al confirmar se ejecuta el loop masivo con OpenAI.</summary>
    private async Task GenerarObservacionesMasivoOpenAIAsync()
    {
        var apiKey = _openAiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = await LocalStorage.GetItemAsync(LsOpenAiKey);
        if (string.IsNullOrWhiteSpace(apiKey?.Trim()))
        {
            await MostrarToastAsync("Configurá la API key de OpenAI en «Configurar API keys».", true);
            return;
        }
        var idsToProcess = _items
            .Where(p => _seleccionados.Contains(p.ProductoID) && (!string.IsNullOrWhiteSpace(p.DescripcionLarga) || !string.IsNullOrWhiteSpace(p.CodigoBarra)))
            .Select(p => p.ProductoID)
            .ToList();
        if (idsToProcess.Count == 0)
        {
            _ = MostrarToastAsync("Seleccioná al menos un producto con descripción o código de barras.", false);
            return;
        }
        AbrirModalOpcionesGeneracion(esMasivo: true);
    }

    /// <summary>Loop masivo de generación con OpenAI usando las opciones elegidas (abreviar corta, autocompletar larga, prompts).</summary>
    private async Task GenerarObservacionesMasivoOpenAIConLoopAsync()
    {
        var apiKey = _openAiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = await LocalStorage.GetItemAsync(LsOpenAiKey);
        if (string.IsNullOrWhiteSpace(apiKey?.Trim()))
        {
            await MostrarToastAsync("Configurá la API key de OpenAI en «Configurar API keys».", true);
            return;
        }
        var idsToProcess = _items
            .Where(p => _seleccionados.Contains(p.ProductoID) && (!string.IsNullOrWhiteSpace(p.DescripcionLarga) || !string.IsNullOrWhiteSpace(p.CodigoBarra)))
            .Select(p => p.ProductoID)
            .ToList();
        if (idsToProcess.Count == 0) return;

        _bulkGeneratingObservaciones = true;
        _bulkGeneratingObservacionesTotal = idsToProcess.Count;
        _bulkGeneratingObservacionesCurrent = 0;
        await InvokeAsync(StateHasChanged);
        var okCount = 0;
        try
        {
            for (int i = 0; i < idsToProcess.Count; i++)
            {
                var p = _items.FirstOrDefault(x => x.ProductoID == idsToProcess[i]);
                if (p == null) { _bulkGeneratingObservacionesCurrent = i + 1; await InvokeAsync(StateHasChanged); continue; }
                var desc = p.DescripcionLarga ?? "";
                var barra = p.CodigoBarra ?? "";
                var prompt = BuildPromptObservacionesYDescripcionesOpenAI(desc, barra, _opcionesAbreviarCorta, _opcionesAutocompletarLarga);
                var result = await OpenAITextService.GenerateTextAsync(prompt, apiKey);
                if (result != null && result.Success && !string.IsNullOrWhiteSpace(result.Text))
                {
                    ParseObservacionesYDescripcionesResponse(result.Text, _opcionesAbreviarCorta, _opcionesAutocompletarLarga,
                        out var observacionPlano, out var corta, out var larga);
                    p.Observaciones = PlainTextObservacionesToRtf(observacionPlano);
                    p.ObservacionesPendienteGuardar = true;
                    if (corta != null) { p.DescripcionCorta = corta; p.DescripcionCortaPendienteGuardar = true; }
                    if (larga != null) { p.DescripcionLarga = larga; p.DescripcionLargaPendienteGuardar = true; }
                    okCount++;
                }
                _bulkGeneratingObservacionesCurrent = i + 1;
                await InvokeAsync(StateHasChanged);
            }
            await MostrarToastAsync(
                okCount > 0 ? $"Se generaron {okCount} producto(s) con OpenAI. Usá «Guardar cambios»." : "No se pudo generar con OpenAI.",
                okCount == 0);
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
