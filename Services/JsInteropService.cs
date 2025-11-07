using Microsoft.JSInterop;
using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public class JsInteropService : IJsInteropService
{
    private readonly IJSRuntime _jsRuntime;

    public JsInteropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<ImageSize?> GetImageSizesAsync(string selector)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<ImageSize>("getImageSizes", selector);
        }
        catch (JSException ex) when (ex.Message.Contains("getImageSizes") || ex.Message.Contains("undefined"))
        {
            Console.WriteLine("⚠️ getImageSizes no está disponible. Asegúrate de que el script se haya cargado correctamente.");
            Console.WriteLine($"Error: {ex.Message}");
            return null;
        }
        catch (JSException ex)
        {
            Console.WriteLine($"❌ Error JS al obtener tamaños de imagen: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error al obtener tamaños de imagen: {ex.Message}");
            return null;
        }
    }

    public async Task SizeSvgOverImageAsync(string selector, double clientW, double clientH, double naturalW, double naturalH)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("sizeSvgOverImage", selector, clientW, clientH, naturalW, naturalH);
        }
        catch (JSException ex) when (ex.Message.Contains("sizeSvgOverImage") || ex.Message.Contains("undefined"))
        {
            Console.WriteLine("⚠️ sizeSvgOverImage no está disponible. Asegúrate de que el script se haya cargado correctamente.");
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (JSException ex)
        {
            Console.WriteLine($"❌ Error JS al ajustar SVG: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error al ajustar SVG: {ex.Message}");
        }
    }

    public async Task CopyToClipboardAsync(string text)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch
        {
            // Silently fail
        }
    }
}

