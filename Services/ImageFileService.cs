using Microsoft.AspNetCore.Components.Forms;
using System.IO;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Servicio para procesar archivos de imagen y PDF.
/// Implementa el principio de responsabilidad única (SRP) al manejar solo el procesamiento de archivos.
/// </summary>
public class ImageFileService : IImageFileService
{
    private const int MaxFileSize = 20 * 1024 * 1024; // 20MB
    private const int MaxPreviewSize = 1600;
    private readonly string[] AllowedImageTypes = { "image/png", "image/jpeg", "image/jpg", "image/bmp", "image/webp" };
    private readonly string[] AllowedPdfTypes = { "application/pdf" };
    private readonly string[] AllowedTypes;

    /// <summary>
    /// Constructor que inicializa los tipos de archivo permitidos.
    /// </summary>
    public ImageFileService()
    {
        AllowedTypes = AllowedImageTypes.Concat(AllowedPdfTypes).ToArray();
    }

    /// <summary>
    /// Procesa un archivo de imagen o PDF y devuelve los bytes y metadatos necesarios.
    /// </summary>
    /// <param name="e">Evento de cambio de archivo del componente InputFile.</param>
    /// <returns>Resultado con los bytes del archivo, data URL y metadatos, o null si hay error.</returns>
    public async Task<ImageFileResult?> ProcessImageFileAsync(InputFileChangeEventArgs e)
    {
        try
        {
            var file = e.File;
            if (file is null)
            {
                return null;
            }

            // Validar tamaño
            if (file.Size > MaxFileSize)
            {
                throw new InvalidOperationException("El archivo es demasiado grande. Máximo 20MB.");
            }

            // Validar tipo
            var contentType = file.ContentType ?? string.Empty;
            var contentTypeLower = contentType.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(contentTypeLower) || !AllowedTypes.Contains(contentTypeLower))
            {
                var ext = (Path.GetExtension(file.Name) ?? string.Empty).ToLowerInvariant();
                contentTypeLower = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    ".pdf" => "application/pdf",
                    _ => string.Empty
                };
            }

            if (string.IsNullOrWhiteSpace(contentTypeLower) || !AllowedTypes.Contains(contentTypeLower))
            {
                throw new InvalidOperationException($"Tipo de archivo no soportado: {file.ContentType} ({file.Name}). Tipos permitidos: imágenes (PNG, JPG, JPEG, BMP, WEBP) y PDF.");
            }

            // Si es PDF, procesarlo directamente sin redimensionar
            if (AllowedPdfTypes.Contains(contentTypeLower))
            {
                using var pdfStream = new MemoryStream();
                await file.OpenReadStream(maxAllowedSize: MaxFileSize).CopyToAsync(pdfStream);
                var pdfBytes = pdfStream.ToArray();

                if (pdfBytes.Length == 0)
                {
                    throw new InvalidOperationException("El archivo PDF está vacío");
                }

                // Para PDF, no podemos crear un data URL para preview, pero guardamos los bytes
                return new ImageFileResult
                {
                    ImageBytes = pdfBytes,
                    ImageDataUrl = "", // Los PDFs no se pueden mostrar como imagen directamente
                    ImageMimeType = contentTypeLower,
                    FileName = file.Name ?? "",
                    FileSize = file.Size
                };
            }

            // Procesar imagen (no PDF)
            // Leer archivo original
            using var ms = new MemoryStream();
            await file.OpenReadStream(maxAllowedSize: MaxFileSize).CopyToAsync(ms);
            var originalBytes = ms.ToArray();

            if (originalBytes.Length == 0)
            {
                throw new InvalidOperationException("El archivo está vacío");
            }

            // Crear preview redimensionada
            try
            {
                var preview = await file.RequestImageFileAsync(contentTypeLower, MaxPreviewSize, MaxPreviewSize);
                using var msPrev = new MemoryStream();
                await preview.OpenReadStream(maxAllowedSize: MaxFileSize).CopyToAsync(msPrev);
                var previewBytes = msPrev.ToArray();
                var base64Prev = Convert.ToBase64String(previewBytes);
                var dataUrl = $"data:{contentTypeLower};base64,{base64Prev}";

                return new ImageFileResult
                {
                    ImageBytes = previewBytes,
                    ImageDataUrl = dataUrl,
                    ImageMimeType = contentTypeLower,
                    FileName = file.Name ?? "",
                    FileSize = file.Size
                };
            }
            catch
            {
                // Fallback: usar original
                var base64 = Convert.ToBase64String(originalBytes);
                var dataUrl = $"data:{contentTypeLower};base64,{base64}";

                return new ImageFileResult
                {
                    ImageBytes = originalBytes,
                    ImageDataUrl = dataUrl,
                    ImageMimeType = contentTypeLower,
                    FileName = file.Name ?? "",
                    FileSize = file.Size
                };
            }
        }
        catch
        {
            return null;
        }
    }
}

