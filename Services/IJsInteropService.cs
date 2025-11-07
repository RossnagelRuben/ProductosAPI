using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public interface IJsInteropService
{
    Task<ImageSize?> GetImageSizesAsync(string selector);
    Task SizeSvgOverImageAsync(string selector, double clientW, double clientH, double naturalW, double naturalH);
    Task CopyToClipboardAsync(string text);
}

