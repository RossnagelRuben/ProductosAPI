namespace BlazorApp_ProductosAPI.Services;

public interface ILocalStorageService
{
    Task<string?> GetItemAsync(string key);
    Task SetItemAsync(string key, string value);
}

