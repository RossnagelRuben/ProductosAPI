using Microsoft.JSInterop;

namespace BlazorApp_ProductosAPI.Services;

public class LocalStorageService : ILocalStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetItemAsync(string key)
    {
        return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
    }

    public async Task SetItemAsync(string key, string value)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
    }
}

