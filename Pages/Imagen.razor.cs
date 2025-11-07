using BlazorApp_ProductosAPI.Models;
using BlazorApp_ProductosAPI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using System.Linq;

namespace BlazorApp_ProductosAPI.Pages
{
    public partial class Imagen
    {
        [Inject] public ILocalStorageService LocalStorage { get; set; }
        [Inject] public IImageFileService ImageFileService { get; set; }
        [Inject] public IOcrService OcrService { get; set; }
        [Inject] public IGeminiService GeminiService { get; set; }
        [Inject] public IColectorService ColectorService { get; set; }
        [Inject] public IJsInteropService JsInterop { get; set; }

        // Habilita el botón cuando hay texto OCR y una imagen cargada, y no está corriendo IA
        private bool CanRunIA => !IsIaLoading
                                 && ImageBytes is not null
                                 && !string.IsNullOrWhiteSpace(FullOcrText);

        // Habilita el botón "ENVIAR AL COLECTOR" cuando hay productos (IA) extraídos
        private bool CanSendToColector => !IsColectorLoading
                                         && !string.IsNullOrWhiteSpace(ProductosJson)
                                         && !string.IsNullOrWhiteSpace(ColectorToken);

        // ===== Estado UI =====
        private string? BearerToken { get; set; }
        private string? GoogleApiKey { get; set; }
        private string? ColectorToken { get; set; }
        private bool IsOcrLoading { get; set; }
        private bool IsIaLoading { get; set; }
        private bool IsColectorLoading { get; set; }
        private bool IsListandoColector { get; set; }
        private string? Error { get; set; }

        // ===== Estado del Modal del Colector =====
        private bool MostrarModalColector { get; set; } = false;
        private string? DatosColectorModal { get; set; }
        private List<ColectorEncabezado>? ColectorDatos { get; set; }
        private FacturaExtraida? ProductosExtraidos { get; set; }

        // ===== Estado del Modal de Tipo de Operación =====
        private bool MostrarModalTipoOperacion { get; set; } = false;
        private int TipoOperacionSeleccionado { get; set; } = 4; // Valor por defecto: pago
        private int TipoOperacionTemporal { get; set; } = 4; // Variable temporal para capturar la selección

        // ===== Opciones de Tipo de Operación =====
        private readonly List<TipoOperacion> TiposOperacion = new()
    {
        new TipoOperacion { Id = 4, Nombre = "Pago" },
        new TipoOperacion { Id = 10, Nombre = "Inventario" },
        new TipoOperacion { Id = 20, Nombre = "Despacho" },
        new TipoOperacion { Id = 21, Nombre = "Recepción" },
        new TipoOperacion { Id = 150, Nombre = "Etiquetas" },
        new TipoOperacion { Id = 23, Nombre = "Pedido Compra / Reposición" }
    };

        // ===== Estado del mensaje de éxito =====
        private bool MostrarMensajeExito { get; set; } = false;
        private string MensajeExito { get; set; } = "";

        // ===== Estado del div verde de confirmación de imagen =====
        private bool MostrarConfirmacionImagen { get; set; } = false;
        private string NombreArchivoImagen { get; set; } = "";
        private string TamañoArchivoImagen { get; set; } = "";

        // ===== Imagen & Overlay =====
        private ElementReference ImgRef;
        private ElementReference ModalRef;
        private string? ImageDataUrl { get; set; }
        private byte[]? ImageBytes { get; set; }
        private string? ImageMimeType { get; set; }
        private string? HoverText { get; set; }
        private readonly List<PolygonModel> Polygons = new();

        // ===== Resultados =====
        private string FullOcrText = "";
        private string ProductosJson = "";
        private string ProveedorExtraido = "";

        // ===== LocalStorage keys =====
        private const string LS_TOKEN = "img_token";
        private const string LS_GKEY = "img_gkey";
        private const string LS_COLECTOR = "img_colector";

        // ===== Tamaños naturales de la imagen =====
        private double NaturalW, NaturalH;

        protected override async Task OnInitializedAsync()
        {
            BearerToken = await LocalStorage.GetItemAsync(LS_TOKEN);
            GoogleApiKey = await LocalStorage.GetItemAsync(LS_GKEY);
            ColectorToken = await LocalStorage.GetItemAsync(LS_COLECTOR);
        }

        // ===== MANEJO DE ARCHIVOS DE IMAGEN =====
        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            try
            {
                // ===== LIMPIEZA COMPLETA =====
                Error = null;
                FullOcrText = "";
                ProductosJson = "";
                ProveedorExtraido = "";
                Polygons.Clear();
                ImageBytes = null;
                ImageDataUrl = null;
                ImageMimeType = null;

                var result = await ImageFileService.ProcessImageFileAsync(e);
                if (result is null)
                {
                    Error = "No se pudo procesar el archivo.";
                    return;
                }

                ImageBytes = result.ImageBytes;
                ImageDataUrl = result.ImageDataUrl;
                ImageMimeType = result.ImageMimeType;
                NaturalW = 800;
                NaturalH = 600;

                // ===== CONFIRMACIÓN VISUAL =====
                NombreArchivoImagen = result.FileName;
                TamañoArchivoImagen = $"{result.FileSize / 1024} KB";
                MostrarConfirmacionImagen = true;

                _ = OcultarConfirmacionImagenAsync();
            }
            catch (Exception ex)
            {
                Error = $"❌ Error crítico: {ex.Message}";

                // ===== LIMPIEZA TOTAL =====
                ImageBytes = null;
                ImageDataUrl = null;
                ImageMimeType = null;
                FullOcrText = "";
                ProductosJson = "";
                ProveedorExtraido = "";
                Polygons.Clear();
            }
        }

        private async Task OcultarConfirmacionImagenAsync()
        {
            try
            {
                await Task.Delay(3000);
                MostrarConfirmacionImagen = false;
                await InvokeAsync(StateHasChanged);
            }
            catch { }
        }

        private async Task RunOCR()
        {
            Error = null;
            FullOcrText = "";
            Polygons.Clear();

            if (ImageBytes is null) { Error = "Elegí una imagen primero."; return; }
            if (string.IsNullOrWhiteSpace(GoogleApiKey)) { Error = "Pegá la Google API Key."; return; }

            await LocalStorage.SetItemAsync(LS_TOKEN, BearerToken ?? "");
            await LocalStorage.SetItemAsync(LS_GKEY, GoogleApiKey ?? "");

            if (IsOcrLoading) return; // Evitar doble ejecución
            IsOcrLoading = true;

            try
            {
                var result = await OcrService.ProcessOcrAsync(ImageBytes, GoogleApiKey, NaturalW, NaturalH);

                if (!result.Success)
                {
                    Error = result.ErrorMessage;
                    return;
                }

                FullOcrText = result.FullText;
                Polygons.Clear();
                Polygons.AddRange(result.Polygons);

                try
                {
                    var s2 = await JsInterop.GetImageSizesAsync("#ocr-img");
                    if (s2 != null)
                    {
                        await JsInterop.SizeSvgOverImageAsync(".overlay", s2.ClientWidth, s2.ClientHeight, s2.NaturalWidth, s2.NaturalHeight);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Error = $"Excepción OCR: {ex.Message}";
            }
            finally
            {
                IsOcrLoading = false;
            }
        }

        // === USA LA MISMA API KEY que Vision y manda la IMAGEN a Gemini ===
        private async Task ExtraerProductosIA()
        {
            Error = null;
            ProductosJson = "";
            ProveedorExtraido = "";

            if (ImageBytes is null)
            {
                Error = "Primero elegí una imagen válida.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ImageMimeType))
            {
                Error = "Primero elegí una imagen válida.";
                return;
            }

            if (string.IsNullOrWhiteSpace(GoogleApiKey))
            {
                Error = "Pegá la Google API Key (se usa para Vision y Gemini).";
                return;
            }

            await LocalStorage.SetItemAsync(LS_GKEY, GoogleApiKey ?? "");

            if (IsIaLoading) return; // Evitar doble ejecución
            IsIaLoading = true;

            try
            {
                var result = await GeminiService.ExtractProductsAsync(ImageBytes, ImageMimeType, GoogleApiKey);

                if (!result.Success)
                {
                    Error = result.ErrorMessage;
                    return;
                }

                ProductosJson = result.ProductosJson;
                ProveedorExtraido = result.ProveedorJson;
                await JsInterop.CopyToClipboardAsync(ProductosJson);
            }
            catch (Exception ex)
            {
                Error = $"Excepción IA: {ex.Message}";
            }
            finally
            {
                IsIaLoading = false;
            }
        }

        // ===== ENVÍO AL COLECTOR =====
        private async Task EnviarAlColector()
        {
            Error = null;

            if (string.IsNullOrWhiteSpace(ProductosJson))
            {
                Error = "No hay productos para enviar al Colector. Primero extrae productos con IA.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ColectorToken))
            {
                Error = "Pegá el Token de Colector para poder enviar los datos.";
                return;
            }

            await LocalStorage.SetItemAsync(LS_COLECTOR, ColectorToken ?? "");

            if (IsColectorLoading) return;
            IsColectorLoading = true;

            try
            {
                var tipoOperacionFinal = TipoOperacionTemporal;
                var result = await ColectorService.SendToColectorAsync(ProductosJson, tipoOperacionFinal, ColectorToken);

                if (!result.Success)
                {
                    Error = result.ErrorMessage;
                    return;
                }

                // Cerrar la modal de tipo de operación después del envío exitoso
                MostrarModalTipoOperacion = false;

                var fechaHoraFormateada = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                var mensajeExito = $"✅ Datos enviados exitosamente al Colector!\n\n" +
                                  $"⚙️ Tipo de operación: {ObtenerNombreTipoOperacion(tipoOperacionFinal)} (ID: {tipoOperacionFinal})\n" +
                                  $"📅 Fecha y hora: {fechaHoraFormateada}";

                MensajeExito = mensajeExito;
                MostrarMensajeExito = true;
                Error = null;

                _ = Task.Delay(5000).ContinueWith(async _ =>
                {
                    await InvokeAsync(() => { MostrarMensajeExito = false; });
                });
            }
            catch (Exception ex)
            {
                Error = $"❌ Excepción al enviar al Colector: {ex.Message}";
            }
            finally
            {
                IsColectorLoading = false;
            }
        }

        // ===== MOSTRAR PRODUCTOS EXTRAÍDOS EN TABLA =====
        private async Task MostrarProductosEnTabla()
        {
            if (string.IsNullOrWhiteSpace(ProductosJson))
            {
                Error = "No hay productos extraídos para mostrar. Primero extrae productos con IA.";
                return;
            }

            try
            {
                // Limpiar datos del servidor
                ColectorDatos = null;
                DatosColectorModal = null;
                
                // Parsear ProductosJson
                ProductosExtraidos = null;
                var jsonLimpio = ProductosJson.Trim();
                
                // Remover markdown code blocks si existen
                if (jsonLimpio.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    var inicio = jsonLimpio.IndexOf('{');
                    var fin = jsonLimpio.LastIndexOf('}');
                    if (inicio >= 0 && fin > inicio)
                    {
                        jsonLimpio = jsonLimpio.Substring(inicio, fin - inicio + 1);
                    }
                }
                else if (jsonLimpio.StartsWith("```"))
                {
                    var inicio = jsonLimpio.IndexOf('{');
                    var fin = jsonLimpio.LastIndexOf('}');
                    if (inicio >= 0 && fin > inicio)
                    {
                        jsonLimpio = jsonLimpio.Substring(inicio, fin - inicio + 1);
                    }
                }
                
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                    };
                    
                    ProductosExtraidos = System.Text.Json.JsonSerializer.Deserialize<FacturaExtraida>(jsonLimpio, options);
                    Console.WriteLine($"Productos extraídos parseados: {(ProductosExtraidos?.Productos != null ? ProductosExtraidos.Productos.Count : 0)} productos");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al parsear ProductosJson: {ex.Message}");
                    Console.WriteLine($"JSON recibido: {jsonLimpio.Substring(0, Math.Min(200, jsonLimpio.Length))}...");
                    ProductosExtraidos = null;
                }
                
                // Mostrar el modal
                MostrarModalColector = true;
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                Error = $"❌ Error al mostrar productos: {ex.Message}";
            }
        }

        // ===== LISTAR DATOS COLECTADOS =====
        private async Task ListarDatosColectados()
        {
            if (string.IsNullOrWhiteSpace(ProductosJson))
            {
                Error = "No hay productos extraídos con IA. Primero extrae productos con IA.";
                return;
            }

            IsListandoColector = true;
            Error = null;
            
            try
            {
                // Limpiar datos del servidor
                ColectorDatos = null;
                DatosColectorModal = null;
                
                // Parsear los productos extraídos con IA
                ProductosExtraidos = null;
                
                try
                {
                    // Limpiar el JSON (remover markdown code blocks si existen)
                    var jsonLimpio = ProductosJson.Trim();
                    
                    // Remover ```json y ``` si existen
                    if (jsonLimpio.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    {
                        var inicio = jsonLimpio.IndexOf('{');
                        var fin = jsonLimpio.LastIndexOf('}');
                        if (inicio >= 0 && fin > inicio)
                        {
                            jsonLimpio = jsonLimpio.Substring(inicio, fin - inicio + 1);
                        }
                    }
                    else if (jsonLimpio.StartsWith("```"))
                    {
                        var inicio = jsonLimpio.IndexOf('{');
                        var fin = jsonLimpio.LastIndexOf('}');
                        if (inicio >= 0 && fin > inicio)
                        {
                            jsonLimpio = jsonLimpio.Substring(inicio, fin - inicio + 1);
                        }
                    }
                    
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                    };
                    
                    ProductosExtraidos = System.Text.Json.JsonSerializer.Deserialize<FacturaExtraida>(jsonLimpio, options);
                    
                    Console.WriteLine($"Productos extraídos parseados: {(ProductosExtraidos?.Productos != null ? ProductosExtraidos.Productos.Count : 0)} productos");
                    Console.WriteLine($"Proveedor: {(ProductosExtraidos?.Proveedor != null ? ProductosExtraidos.Proveedor.RazonSocial : "null")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al parsear JSON de productos IA: {ex.Message}");
                    if (!string.IsNullOrEmpty(ProductosJson))
                    {
                        var preview = ProductosJson.Length > 200 ? ProductosJson.Substring(0, 200) + "..." : ProductosJson;
                        Console.WriteLine($"JSON recibido: {preview}");
                    }
                    ProductosExtraidos = null;
                    Error = $"Error al parsear los productos: {ex.Message}";
                }
                
                // Mostrar el modal siempre
                MostrarModalColector = true;
                Console.WriteLine($"MostrarModalColector establecido a: {MostrarModalColector}");
                IsListandoColector = false;
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                Error = $"❌ Excepción al listar productos: {ex.Message}";
                IsListandoColector = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        // ===== MANEJO DEL MODAL DEL COLECTOR =====
        private async Task CerrarModalColector()
        {
            MostrarModalColector = false;
            DatosColectorModal = null;
            ColectorDatos = null;
            ProductosExtraidos = null;
            await Task.CompletedTask;
        }

        // ===== MANEJO DEL MODAL DE TIPO DE OPERACIÓN =====
        private async Task AbrirModalTipoOperacion()
        {
            TipoOperacionTemporal = TipoOperacionSeleccionado; // Inicializar con el valor actual
            MostrarModalTipoOperacion = true;
            Console.WriteLine($"🔍 ABRIR MODAL - TipoOperacionTemporal inicializado: {TipoOperacionTemporal}");
            await Task.CompletedTask;
        }

        private async Task CerrarModalTipoOperacion()
        {
            MostrarModalTipoOperacion = false;
            await Task.CompletedTask;
        }

        private async Task SeleccionarTipoOperacion(int tipoId, string tipoNombre)
        {
            Console.WriteLine($"🔍 SELECCIONAR TIPO OPERACIÓN:");
            Console.WriteLine($"🔍 TipoId recibido: {tipoId}");
            Console.WriteLine($"🔍 TipoNombre recibido: {tipoNombre}");
            Console.WriteLine($"🔍 TipoOperacionTemporal ANTES del cambio: {TipoOperacionTemporal}");

            TipoOperacionTemporal = tipoId;

            Console.WriteLine($"🔍 TipoOperacionTemporal DESPUÉS del cambio: {TipoOperacionTemporal}");

            // Forzar actualización del estado
            await InvokeAsync(StateHasChanged);

            Console.WriteLine($"🔍 Estado actualizado. TipoOperacionTemporal final: {TipoOperacionTemporal}");
        }

        private async Task ConfirmarTipoOperacion()
        {
            Console.WriteLine($"🔍 CONFIRMAR TIPO OPERACIÓN:");
            Console.WriteLine($"🔍 TipoOperacionTemporal: {TipoOperacionTemporal}");
            Console.WriteLine($"🔍 TipoOperacionSeleccionado ANTES del cambio: {TipoOperacionSeleccionado}");

            // Asignar el valor temporal al valor final
            TipoOperacionSeleccionado = TipoOperacionTemporal;

            Console.WriteLine($"🔍 TipoOperacionSeleccionado DESPUÉS del cambio: {TipoOperacionSeleccionado}");
            Console.WriteLine($"🔍 VALOR FINAL QUE SE USARÁ: {TipoOperacionSeleccionado}");

            // Forzar actualización del estado
            await InvokeAsync(StateHasChanged);

            // No cerrar la modal aquí, se cerrará después del envío exitoso
            await EnviarAlColector();
        }

        private string ObtenerNombreTipoOperacion(int tipoId)
        {
            var tipo = TiposOperacion.FirstOrDefault(t => t.Id == tipoId);
            return tipo?.Nombre ?? $"ID: {tipoId}";
        }

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Escape")
            {
                await CerrarModalColector();
            }
        }

        private async Task CopiarDatosColector()
        {
            if (!string.IsNullOrWhiteSpace(DatosColectorModal))
            {
                await JsInterop.CopyToClipboardAsync(DatosColectorModal);
            }
        }

        private async Task CopyOcrText()
        {
            if (!string.IsNullOrWhiteSpace(FullOcrText))
            {
                await JsInterop.CopyToClipboardAsync(FullOcrText);
            }
        }

        private async Task CopyText(string txt)
        {
            await JsInterop.CopyToClipboardAsync(txt ?? "");
            HoverText = "📋 Copiado al portapapeles.\n\n" + txt;
        }


        private async Task OnImageLoaded()
        {
            try
            {
                if (ImageBytes == null || ImageDataUrl == null) return;

                var s = await JsInterop.GetImageSizesAsync("#ocr-img");

                if (s != null)
                {
                    NaturalW = s.NaturalWidth;
                    NaturalH = s.NaturalHeight;

                    await JsInterop.SizeSvgOverImageAsync(".overlay",
                        s.ClientWidth, s.ClientHeight, s.NaturalWidth, s.NaturalHeight);
                }
            }
            catch { }
        }
    }
}