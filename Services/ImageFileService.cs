using Microsoft.AspNetCore.Components.Forms;
using System.IO;

namespace BlazorApp_ProductosAPI.Services;

public class ImageFileService : IImageFileService
{
    private const int MaxFileSize = 20 * 1024 * 1024; // 20MB
    private const int MaxPreviewSize = 1600;
    private readonly string[] AllowedTypes = { "image/png", "image/jpeg", "image/jpg", "image/bmp", "image/webp" };

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
                    _ => string.Empty
                };
            }

            if (string.IsNullOrWhiteSpace(contentTypeLower) || !AllowedTypes.Contains(contentTypeLower))
            {
                throw new InvalidOperationException($"Tipo de archivo no soportado: {file.ContentType} ({file.Name})");
            }

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

