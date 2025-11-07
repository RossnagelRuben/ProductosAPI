using Microsoft.AspNetCore.Components.Forms;

namespace BlazorApp_ProductosAPI.Services;

public interface IImageFileService
{
    Task<ImageFileResult?> ProcessImageFileAsync(InputFileChangeEventArgs e);
}

public sealed class ImageFileResult
{
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
    public string ImageDataUrl { get; set; } = "";
    public string ImageMimeType { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
}

