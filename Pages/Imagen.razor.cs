using BlazorApp_ProductosAPI.Models;
using BlazorApp_ProductosAPI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

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

        // Habilita el botón cuando hay texto OCR y una imagen/PDF cargada, y no está corriendo IA
        // Para PDFs, no necesitamos OCR previo ya que Gemini puede procesarlos directamente
        private bool CanRunIA => !IsIaLoading
                                 && ImageBytes is not null
                                 && (!string.IsNullOrWhiteSpace(FullOcrText) || IsPdfFile);

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

        /// <summary>
        /// Indica si el archivo cargado es un PDF.
        /// </summary>
        private bool IsPdfFile => !string.IsNullOrWhiteSpace(ImageMimeType) && 
                                  ImageMimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

        // ===== Resultados =====
        private string FullOcrText = "";
        private string ProductosJson = "";
        private string ProveedorExtraido = "";
        private string? TipoComprobanteDetectado { get; set; }

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
                TipoComprobanteDetectado = null;

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
                
                // Mensaje específico según el tipo de archivo
                if (IsPdfFile)
                {
                    MensajeExito = $"✅ PDF cargado: {NombreArchivoImagen} ({TamañoArchivoImagen})\nPuedes extraer productos directamente con IA.";
                }
                else
                {
                    MensajeExito = $"✅ Imagen cargada: {NombreArchivoImagen} ({TamañoArchivoImagen})";
                }

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

        /// <summary>
        /// Ejecuta OCR sobre la imagen usando Google Vision API.
        /// Nota: Los PDFs no se procesan con OCR, se envían directamente a Gemini.
        /// </summary>
        private async Task RunOCR()
        {
            Error = null;
            FullOcrText = "";
            Polygons.Clear();

            if (ImageBytes is null) { Error = "Elegí una imagen primero."; return; }
            if (IsPdfFile) { Error = "Los PDFs no requieren OCR. Usa directamente 'Extraer productos (IA)'."; return; }
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
                DetectarTipoComprobanteDesdeOcr();

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

        /// <summary>
        /// Extrae productos y datos de la factura/comprobante usando Gemini AI.
        /// Funciona tanto con imágenes como con PDFs.
        /// </summary>
        private async Task ExtraerProductosIA()
        {
            Error = null;
            ProductosJson = "";
            ProveedorExtraido = "";

            if (ImageBytes is null)
            {
                Error = "Primero elegí una imagen o PDF válido.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ImageMimeType))
            {
                Error = "Primero elegí una imagen o PDF válido.";
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
                
                // Normalizar talonario y número de comprobante si es necesario
                try
                {
                    var jsonLimpio = LimpiarJsonPosiblesCodeBlocks(ProductosJson);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    
                    var factura = JsonSerializer.Deserialize<FacturaExtraida>(jsonLimpio, options);
                    if (factura != null)
                    {
                        NormalizarTalonarioYNumero(factura);
                        NormalizarFechaISO8601(factura);
                        AsignarOrdenSiFalta(factura);
                        ProductosJson = JsonSerializer.Serialize(factura, new JsonSerializerOptions { WriteIndented = true });
                    }
                }
                catch
                {
                    // Si falla la normalización, continuar con el JSON original
                }
                
                DetectarTipoComprobanteDesdeJson();
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
                var jsonLimpio = LimpiarJsonPosiblesCodeBlocks(ProductosJson);
                
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    
                    ProductosExtraidos = JsonSerializer.Deserialize<FacturaExtraida>(jsonLimpio, options);
                    NormalizarTalonarioYNumero(ProductosExtraidos);
                    NormalizarFechaISO8601(ProductosExtraidos);
                    AsignarOrdenSiFalta(ProductosExtraidos);
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
                    var jsonLimpio = LimpiarJsonPosiblesCodeBlocks(ProductosJson);
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    
                    ProductosExtraidos = JsonSerializer.Deserialize<FacturaExtraida>(jsonLimpio, options);
                    NormalizarTalonarioYNumero(ProductosExtraidos);
                    NormalizarFechaISO8601(ProductosExtraidos);
                    AsignarOrdenSiFalta(ProductosExtraidos);
                    
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

        private void DetectarTipoComprobanteDesdeJson()
        {
            var tipoDesdeJson = ObtenerTipoComprobanteDesdeJson(ProductosJson);
            if (!string.IsNullOrWhiteSpace(tipoDesdeJson))
            {
                TipoComprobanteDetectado = tipoDesdeJson;
            }
            else
            {
                DetectarTipoComprobanteDesdeOcr();
            }
        }

        private void DetectarTipoComprobanteDesdeOcr()
        {
            TipoComprobanteDetectado = DetectarTipoComprobante(FullOcrText);
        }

        private static string? ObtenerTipoComprobanteDesdeJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var jsonLimpio = LimpiarJsonPosiblesCodeBlocks(json);
                using var doc = JsonDocument.Parse(jsonLimpio);
                if (doc.RootElement.TryGetProperty("encabezado", out var encabezado) &&
                    encabezado.ValueKind == JsonValueKind.Object &&
                    encabezado.TryGetProperty("tipo_comprobante", out var tipoElement) &&
                    tipoElement.ValueKind == JsonValueKind.String)
                {
                    return tipoElement.GetString();
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string? DetectarTipoComprobante(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return null;
            }

            var normalized = NormalizarTexto(texto);

            // PRIORIDAD 1: Detectar "NO VALIDO COMO FACTURA" o "COMPROBANTE NO VALIDO" -> "X"
            // Esto debe ir ANTES de cualquier detección de factura para evitar falsos positivos
            if (normalized.Contains("NO VALIDO COMO FACTURA") ||
                normalized.Contains("NO VÁLIDO COMO FACTURA") ||
                normalized.Contains("COMPROBANTE NO VALIDO") ||
                normalized.Contains("COMPROBANTE NO VÁLIDO"))
            {
                return "X";
            }

            // PRIORIDAD 2: Detectar "X" o "COMPROBANTE X" -> "OTROS"
            if (normalized.Contains("COMPROBANTE X") || 
                normalized.Contains("COMPROBANTE TIPO X") ||
                normalized.Contains("COMPROBANTE- X") ||
                (normalized.Contains("COMPROBANTE") && normalized.Contains(" X ")) ||
                (normalized.Contains(" X ") && !normalized.Contains("FACTURA")))
            {
                return "OTROS";
            }

            // PRIORIDAD 3: Detectar "CREDITO" (puede ser nota de crédito A, B o C)
            // Primero verificar si tiene letra específica (A, B o C)
            var notaCreditoConTipo = DetectarNotaConLetra(normalized, "NOTA DE CREDITO", "Nota de crédito");
            if (!string.IsNullOrWhiteSpace(notaCreditoConTipo))
            {
                return notaCreditoConTipo;
            }

            // Si solo dice "CREDITO" o "NOTA DE CREDITO" sin letra
            if (normalized.Contains("CREDITO") || normalized.Contains("NOTA DE CREDITO"))
            {
                // Verificar si hay una letra A, B o C cerca de "CREDITO"
                var tipos = new[] { 'A', 'B', 'C' };
                foreach (var tipo in tipos)
                {
                    if (normalized.Contains($"CREDITO {tipo}") || 
                        normalized.Contains($"CREDITO-{tipo}") ||
                        normalized.Contains($"CREDITO \"{tipo}\"") ||
                        normalized.Contains($"CREDITO '{tipo}'") ||
                        normalized.Contains($"NOTA DE CREDITO {tipo}") ||
                        normalized.Contains($"NOTA DE CREDITO-{tipo}") ||
                        normalized.Contains($"NOTA DE CREDITO \"{tipo}\"") ||
                        normalized.Contains($"NOTA DE CREDITO '{tipo}'") ||
                        normalized.Contains($"COMPROBANTE NOTA DE CREDITO {tipo}"))
                    {
                        return $"Nota de crédito {tipo}";
                    }
                }
                return "Nota de crédito";
            }

            // PRIORIDAD 4: Detectar notas de débito
            var notaDebitoConTipo = DetectarNotaConLetra(normalized, "NOTA DE DEBITO", "Nota de débito");
            if (!string.IsNullOrWhiteSpace(notaDebitoConTipo))
            {
                return notaDebitoConTipo;
            }

            if (normalized.Contains("NOTA DE DEBITO"))
            {
                return "Nota de débito";
            }

            // PRIORIDAD 5: Detectar presupuesto
            if (normalized.Contains("PRESUPUESTO"))
            {
                return "Presupuesto";
            }

            // PRIORIDAD 6: Detectar facturas A, B, C (solo si NO dice "NO VALIDO" ni "X")
            // Verificar que no sea un comprobante no válido antes de detectar factura
            if (!normalized.Contains("NO VALIDO") && 
                !normalized.Contains("NO VÁLIDO") &&
                !normalized.Contains("COMPROBANTE X") &&
                !normalized.Contains("COMPROBANTE TIPO X"))
            {
                if (ContieneFacturaTipo(normalized, 'A'))
                {
                    return "Factura A";
                }

                if (ContieneFacturaTipo(normalized, 'B'))
                {
                    return "Factura B";
                }

                if (ContieneFacturaTipo(normalized, 'C'))
                {
                    return "Factura C";
                }
            }

            // PRIORIDAD 7: Detectar remito
            if (normalized.Contains("REMITO"))
            {
                return "Remito";
            }

            // PRIORIDAD 8: Detectar ticket
            if (normalized.Contains("TICKET") || normalized.Contains("TIQUE") || normalized.Contains("TIQUET"))
            {
                return "Ticket";
            }

            return null;
        }

        private static bool ContieneFacturaTipo(string texto, char letra)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return false;
            }

            var patrones = new[]
            {
                $"FACTURA {letra}",
                $"FACTURA-{letra}",
                $"FACTURA \"{letra}\"",
                $"FACTURA '{letra}'",
                $"FACTURA ELECTRONICA {letra}",
                $"FACTURA TIPO {letra}",
                $"FACT. {letra}"
            };

            return patrones.Any(texto.Contains);
        }

        private static string? DetectarNotaConLetra(string texto, string fraseBase, string etiquetaBase)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return null;
            }

            var tipos = new[] { 'A', 'B', 'C' };

            foreach (var tipo in tipos)
            {
                var patrones = new[]
                {
                    $"{fraseBase} {tipo}",
                    $"{fraseBase}-{tipo}",
                    $"{fraseBase} \"{tipo}\"",
                    $"{fraseBase} '{tipo}'",
                    $"{fraseBase} TIPO {tipo}",
                    $"{fraseBase} ELECTRONICA {tipo}",
                    $"{fraseBase} COMPROBANTE {tipo}",
                    $"COMPROBANTE {fraseBase} {tipo}"
                };

                if (patrones.Any(texto.Contains))
                {
                    return $"{etiquetaBase} {tipo}";
                }
            }

            return null;
        }

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            var normalized = texto.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToUpperInvariant(c));
                }
            }

            return builder.ToString();
        }

        private static string LimpiarJsonPosiblesCodeBlocks(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            var jsonLimpio = json.Trim();

            if (!jsonLimpio.StartsWith("```", StringComparison.Ordinal))
            {
                return jsonLimpio;
            }

            var inicio = jsonLimpio.IndexOf('{');
            var fin = jsonLimpio.LastIndexOf('}');

            if (inicio >= 0 && fin > inicio)
            {
                return jsonLimpio.Substring(inicio, fin - inicio + 1);
            }

            return jsonLimpio;
        }

        /// <summary>
        /// Normaliza el talonario y número de comprobante.
        /// Si el talonario es null/vacío pero el número de comprobante tiene formato "0008-00016356",
        /// extrae el talonario (antes del guion) y actualiza el número de comprobante (después del guion).
        /// </summary>
        private static void NormalizarTalonarioYNumero(FacturaExtraida? factura)
        {
            if (factura?.Encabezado == null)
            {
                return;
            }

            var encabezado = factura.Encabezado;

            // Si el talonario está vacío pero el número de comprobante tiene el formato esperado
            if (string.IsNullOrWhiteSpace(encabezado.Talonario) && 
                !string.IsNullOrWhiteSpace(encabezado.NumeroComprobante))
            {
                var numeroCompleto = encabezado.NumeroComprobante.Trim();
                var partes = numeroCompleto.Split('-', StringSplitOptions.RemoveEmptyEntries);

                if (partes.Length == 2)
                {
                    // Extraer talonario (antes del guion) y número (después del guion)
                    encabezado.Talonario = partes[0].Trim();
                    encabezado.NumeroComprobante = partes[1].Trim();
                }
            }
        }

        /// <summary>
        /// Normaliza la fecha del comprobante al formato ISO 8601 completo con zona horaria (YYYY-MM-DDTHH:mm:ss+00:00).
        /// Intenta parsear la fecha en varios formatos comunes y la convierte a ISO 8601 completo.
        /// </summary>
        private static void NormalizarFechaISO8601(FacturaExtraida? factura)
        {
            if (factura?.Encabezado == null || string.IsNullOrWhiteSpace(factura.Encabezado.FechaComprobante))
            {
                return;
            }

            var fechaOriginal = factura.Encabezado.FechaComprobante.Trim();

            // Si ya está en formato ISO 8601 completo con zona horaria, validar y mantener
            if (System.Text.RegularExpressions.Regex.IsMatch(fechaOriginal, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$") ||
                System.Text.RegularExpressions.Regex.IsMatch(fechaOriginal, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$"))
            {
                // Validar que sea una fecha válida
                if (DateTimeOffset.TryParse(fechaOriginal, out var fechaValidada))
                {
                    factura.Encabezado.FechaComprobante = fechaValidada.ToString("yyyy-MM-ddTHH:mm:sszzz");
                    return;
                }
            }

            // Si está en formato ISO 8601 sin zona horaria (YYYY-MM-DD o YYYY-MM-DDTHH:mm:ss)
            if (System.Text.RegularExpressions.Regex.IsMatch(fechaOriginal, @"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}:\d{2})?$"))
            {
                if (DateTime.TryParse(fechaOriginal, out var fechaSinZona))
                {
                    // Convertir a DateTimeOffset con la zona horaria local y luego a formato ISO 8601 completo
                    var fechaConZona = new DateTimeOffset(fechaSinZona, TimeZoneInfo.Local.GetUtcOffset(fechaSinZona));
                    factura.Encabezado.FechaComprobante = fechaConZona.ToString("yyyy-MM-ddTHH:mm:sszzz");
                    return;
                }
            }

            // Intentar parsear en varios formatos comunes
            var formatos = new[]
            {
                "dd/MM/yyyy",
                "dd-MM-yyyy",
                "dd.MM.yyyy",
                "d/M/yyyy",
                "d-M-yyyy",
                "d.M.yyyy",
                "dd/MM/yy",
                "dd-MM-yy",
                "dd.MM.yy",
                "yyyy/MM/dd",
                "yyyy-MM-dd",
                "yyyy.MM.dd",
                "MM/dd/yyyy",
                "MM-dd-yyyy",
                "MM.dd.yyyy"
            };

            foreach (var formato in formatos)
            {
                if (DateTime.TryParseExact(fechaOriginal, formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaParseada))
                {
                    // Convertir a DateTimeOffset con la zona horaria local y luego a formato ISO 8601 completo
                    var fechaConZona = new DateTimeOffset(fechaParseada, TimeZoneInfo.Local.GetUtcOffset(fechaParseada));
                    factura.Encabezado.FechaComprobante = fechaConZona.ToString("yyyy-MM-ddTHH:mm:sszzz");
                    return;
                }
            }

            // Si no se pudo parsear con formatos específicos, intentar parseo genérico
            if (DateTime.TryParse(fechaOriginal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaGenerica))
            {
                // Convertir a DateTimeOffset con la zona horaria local y luego a formato ISO 8601 completo
                var fechaConZona = new DateTimeOffset(fechaGenerica, TimeZoneInfo.Local.GetUtcOffset(fechaGenerica));
                factura.Encabezado.FechaComprobante = fechaConZona.ToString("yyyy-MM-ddTHH:mm:sszzz");
                return;
            }

            // Si no se pudo parsear, intentar con DateTimeOffset directamente
            if (DateTimeOffset.TryParse(fechaOriginal, out var fechaOffset))
            {
                factura.Encabezado.FechaComprobante = fechaOffset.ToString("yyyy-MM-ddTHH:mm:sszzz");
                return;
            }

            // Si no se pudo parsear, dejar la fecha original (pero intentar limpiar espacios)
            factura.Encabezado.FechaComprobante = fechaOriginal;
        }

        /// <summary>
        /// Asigna el orden automáticamente a los productos que no lo tengan.
        /// Si un producto no tiene orden, se le asigna según su posición en la lista (1, 2, 3, etc.).
        /// </summary>
        private static void AsignarOrdenSiFalta(FacturaExtraida? factura)
        {
            if (factura?.Productos == null || !factura.Productos.Any())
            {
                return;
            }

            var orden = 1;
            foreach (var producto in factura.Productos)
            {
                if (producto.Orden == null || producto.Orden <= 0)
                {
                    producto.Orden = orden;
                }
                orden++;
            }
        }
    }
}