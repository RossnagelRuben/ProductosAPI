using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorApp_ProductosAPI.Models.AsignarImagenes;

namespace BlazorApp_ProductosAPI.Components.AsignarImagenes;

public partial class ProductosImagenGrid
{
    private HashSet<int> _imagenFallidaIds = new();
    private int _lastBusquedaId = -1;

    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter] public IEnumerable<ProductoConImagenDto>? Items { get; set; }
    [Parameter] public int BusquedaId { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string LoadingText { get; set; } = "Cargando productos…";
    [Parameter] public string EmptyText { get; set; } = "No hay productos para mostrar. Ajusta los filtros y busca.";
    /// <summary>Placeholder cuando no hay URL o la imagen falla al cargar. SVG codificado para que se visualice correctamente.</summary>
    [Parameter] public string PlaceholderImageUrl { get; set; } = "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22200%22 height=%22200%22 viewBox=%220 0 200 200%22%3E%3Crect fill=%22%23e9ecef%22 width=%22200%22 height=%22200%22/%3E%3Ctext x=%22100%22 y=%22105%22 text-anchor=%22middle%22 fill=%22%23999%22 font-size=%2214%22%3ESin imagen%3C/text%3E%3C/svg%3E";
    [Parameter] public int? IsLoadingImage { get; set; }
    [Parameter] public int? IsSavingImage { get; set; }
    [Parameter] public EventCallback<ProductoConImagenDto> OnAbrirModalImagen { get; set; }
    [Parameter] public EventCallback<ProductoConImagenDto> OnGuardar { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (BusquedaId != _lastBusquedaId)
        {
            _lastBusquedaId = BusquedaId;
            _imagenFallidaIds.Clear();
        }
        if (Items != null && Items.Any())
        {
            foreach (var p in Items)
            {
                if (!string.IsNullOrWhiteSpace(p.ImagenUrl) && EsDataUrlDeImagenReal(p.ImagenUrl))
                    _imagenFallidaIds.Remove(p.ProductoID);
            }
            await LogGridAlConsolaAsync();
        }
    }

    private static bool EsDataUrlDeImagenReal(string url)
    {
        return url.StartsWith("data:image/png", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("data:image/jpeg", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("data:image/jpg", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("data:image/gif", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("data:image/webp", StringComparison.OrdinalIgnoreCase);
    }

    private string GetImageSrc(ProductoConImagenDto p)
    {
        if (_imagenFallidaIds.Contains(p.ProductoID) || string.IsNullOrWhiteSpace(p.ImagenUrl))
            return PlaceholderImageUrl;
        return p.ImagenUrl;
    }

    private async Task LogGridAlConsolaAsync()
    {
        try
        {
            var list = Items!.Select(p =>
            {
                var src = GetImageSrc(p);
                return new
                {
                    p.ProductoID,
                    p.Codigo,
                    ImagenUrl = p.ImagenUrl != null ? (p.ImagenUrl.Length > 100 ? p.ImagenUrl.Substring(0, 100) + "…" : p.ImagenUrl) : "(null)",
                    srcUsado = src.StartsWith("data:") ? "(data URL placeholder)" : (src.Length > 100 ? src.Substring(0, 100) + "…" : src),
                    usaPlaceholder = string.IsNullOrWhiteSpace(p.ImagenUrl) || _imagenFallidaIds.Contains(p.ProductoID)
                };
            }).ToList();
            var json = JsonSerializer.Serialize(list);
            await JS.InvokeVoidAsync("__logAsignarImagenes", "Grid: src de cada <img>", json);
        }
        catch { /* no romper la UI */ }
    }

    private async Task MarcarImagenFallida(ProductoConImagenDto p)
    {
        if (string.IsNullOrWhiteSpace(p.ImagenUrl) || p.ImagenUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase))
            return;
        if (p.ImagenUrl.Equals(PlaceholderImageUrl, StringComparison.Ordinal))
            return;
        try
        {
            await JS.InvokeVoidAsync("__logAsignarImagenes", "Imagen FALLÓ al cargar (onerror)", JsonSerializer.Serialize(new
            {
                p.ProductoID,
                p.Codigo,
                ImagenUrl = p.ImagenUrl.Length > 120 ? p.ImagenUrl.Substring(0, 120) + "…" : p.ImagenUrl
            }));
        }
        catch { }
        _imagenFallidaIds.Add(p.ProductoID);
        StateHasChanged();
    }
}
